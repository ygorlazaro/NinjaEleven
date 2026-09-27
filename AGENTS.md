# Ninja Eleven — Development Guide

## Project Structure

```
frontend/ninja-eleven-web/    React + TypeScript frontend
backend/                          .NET 10 backend (Api, Application, Domain, Infrastructure)
tests/                            xUnit test projects
```

## Frontend Commands

```bash
cd frontend/ninja-eleven-web
npm install         # install dependencies
npm run dev         # start Vite dev server (http://localhost:5173)
npm run build       # build for production
npm run lint        # run ESLint
npm run faces:generate  # redraw the pool of player faces into the backend's embedded resource
```

The dev server proxies `/api` and `/matchHub` to the .NET backend at `http://localhost:5100`.

### Frontend environment

`src/config/env.ts` is the only place that knows the backend address:

- `VITE_API_BASE_URL` — REST base URL, falls back to `http://localhost:5100`
- `VITE_HUB_BASE_URL` — SignalR base URL, falls back to `VITE_API_BASE_URL`

Copy `.env.example` to `.env.local` to override. Setting `VITE_API_BASE_URL=/api`
sends the calls through the Vite proxy instead of directly to the API. The API
enables CORS for `http://localhost:5173`, so direct calls work in development.

## Backend Commands

```bash
cd backend
dotnet build                          # build the solution
dotnet run --project NinjaEleven.Api  # run the API
dotnet test                           # run all tests
dotnet ef migrations add <Name>       # add a migration
dotnet ef database update             # apply migrations
dotnet ef migrations add <Name> --project NinjaEleven.Infrastructure --startup-project NinjaEleven.Infrastructure
```

`dotnet test` does not rebuild the API, so an API started with `--no-build` keeps its own
copy of the Domain, Application and Infrastructure assemblies and can be running code that is
several edits old. `dotnet build` before restarting it, or the check you are making is of the
wrong binary.

The API runs on `http://localhost:5100` with Hot Reload enabled.

## Database

PostgreSQL 18 in Docker, listening on port `5433` (database `football_manager`):

```bash
docker start postgres     # start if it is stopped
docker logs postgres
```

Connection string lives in `backend/NinjaEleven.Api/appsettings.json` under
`ConnectionStrings:NinjaEleven`. The database itself is still called `football_manager`
on purpose: it is where the existing career lives, and renaming it would throw that
career away.

On startup the API runs `MigrateAsync` and then `IDataSeeder`. The seeder is idempotent:
it fills the `names`/`surnames` pools and creates the starting world (1 season,
1 competition, 8 teams, 23 players each) only when the `teams` table is empty.

## REST Contract

All routes are singular. Controllers only map DTOs; the flow is
`Controller → Service → Repository → Database`.

```text
GET  /team                                  GET  /round
GET  /team/{id}                             GET  /round/{id}
GET  /team/{teamId}/squad/{seasonId}         GET  /round/by-competition-season/{id}

GET  /player                                GET  /fixture
GET  /player/{id}                           GET  /fixture/{id}
GET  /player/{playerId}/season/{seasonId}   GET  /fixture/by-round/{roundId}

GET  /competition                           GET  /match
GET  /competition/{id}                      GET  /match/{id}
GET  /competition/by-season/{seasonId}      GET  /match/{id}/event?afterSequence=
GET  /competition/{editionId}/club          POST /match
POST /competition                           GET  /match/tactics
                                          GET  /match/round-report/{roundId}
                                          GET  /match/squad-suggestion?teamId=&seasonId=&tacticCode=

GET  /season                                POST /league/setup
GET  /season/{id}                           GET  /league/standing/{competitionSeasonId}
GET  /season/current                        GET  /league/standing/{competitionSeasonId}/team/{teamId}
POST /season                                GET  /league/scorer/{seasonId}?topN=
GET  /player/{id}/profile?seasonId=
```

Domain errors return RFC 7807 with a stable `code` (e.g. `TeamNotFound`,
`SeasonRequired`, `NotEnoughTeams`); the frontend never parses messages to learn rules.
Malformed requests (an id that is not a guid, a missing field) answer 400 with
`code: ValidationFailed`. `POST /league/setup` is idempotent: when the edition already
has a schedule it returns the existing one instead of creating a second one.

The flow is: pick a club (`#/`), read the squad (`#/team/{teamId}`), see the fixtures
(`#/league`), choose the eleven (`#/match/lineup/{fixtureId}`), then play
(`#/match/{matchId}`). A career opens on the club, not on the table: the manager's own
squad is the first thing he reads and the fixtures are one click away. The lineup screen
sends the chosen eleven with `POST /match/start/{fixtureId}`; the backend validates it
again and answers `InvalidLineup`, `GoalkeeperRequired` or `PlayerNotAvailable`, so the
client can never force an illegal eleven. The opponent's eleven is picked by the
backend.

The match engine lives in the domain (`MatchEngine` + `MatchState`) and is driven by
`MatchService` over REST. `POST /match/start/{fixtureId}` creates the session and locks
the starting eleven, then `/match/tick`, `/match/continue-second-half`,
`/match/substitution`, `/match/penalty-taker`, `/match/pause`, `/match/resume` and
`/match/speed` drive it; `/match/lineup`, `/match/state`, `/match/result` and
`/match/{id}/event` read it. The live working memory of a running match is held in
memory by `IMatchSessionRegistry`; the persisted `Match` row is the source of truth for
history and is written on every tick.

SignalR (`/matchHub`) is the live channel. `MatchHub` is thin: command DTO in, service
call, DTOs out — no football rules, no repository access. `MatchLoopService`
(`BackgroundService`) is the single simulation loop: it walks the active sessions, asks
the service for one tick each and republishes the result through `IMatchBroadcaster`, so
a match is never advanced by two callers at once. Events carry `Match.Sequence` so a
client can detect gaps; a reconnecting client re-subscribes, which returns the current
snapshot before the next event. Pause, resume and half-time are client driven: the loop
stops on its own at half-time and on a finished match.

A client follows a whole matchday on the same connection: `MatchGroup` carries the match it
is watching in full, and `RoundGroup` carries the score *and the events* of the other
matches. The second part is why `MatchdayEvent` exists. A scoreboard that can only say
1 x 0 does not say who scored, and a round the manager is not playing in is still a round
he is watching. The events sent to the round group are the same events the match told its
own followers — a matchday that reworded the other clubs' goals would be four matches and
five accounts of the same afternoon. Two consequences worth keeping:

- **The match id is part of the payload.** A client following four matches receives all
  four in one stream, and a log without an owner is a log that gets merged into the wrong
  match. `MatchdayEventDto.MatchId` is what keeps the four apart.
- **A goal in another match is silent.** `useMatchAudio` is bound to the watched match's
  own feed, and a goal next door has no business sounding in the manager's stadium.

**A hub argument is the argument, not a wrapper around it.** SignalR binds by position:
`SubscribeMatchday(Guid roundId)` is invoked as `invoke('SubscribeMatchday', roundId)` and
never as `{ roundId }`, while a method taking a DTO is invoked with the DTO *as* its one
argument. Handing a `Guid` parameter an object is refused by the binder with "Parameters to
hub method are incorrect", and the refusal is invisible to the manager: the scoreboard simply
never moves, while the backend publishes every other match to a group nobody is in. It fails
silently because the only thing that changes is the absence of a stream, and a screen with
one frozen panel looks like a match that is going badly.

**Joining the round is an effect of its own.** The round id arrives from the fixture list,
after the screen mounts, so an effect that reads it must depend on it. A subscription asked
for inside the match's own effect tests an empty string, never joins, and is never retried,
because that effect does not run again.

The REST match commands and the hub expose the same behaviour, so the game works over
REST alone and SignalR only replaces polling.

## The Pyramid

Brazil is not one league, and the code stopped pretending otherwise in three divisions of
twelve. Three things follow from that, and they are the three things that were got wrong first:

- **A club has no division. An edition has a division.** `Division` is permanent and a club
  belongs to one for a season through `competition_participants`, so a club that is relegated
  is the same club next season in a different division. A `divisionId` on `teams` would make
  the move delete the club.
- **A competition is not an edition.** `POST /league/setup` used to take a list of clubs and
  a season, which is a request that cannot say which of the three divisions it means. The
  client asks `GET /competition/by-season/{id}` for the editions, picks one, and reads its
  clubs from `GET /competition/{editionId}/club`. A client that joins a club list to a
  division list itself is how a club ends up in the 3ª Divisão.
- **A table is official or projected, and the backend says which.** `GET /league/standing/{id}`
  returns both, plus `hasLiveMatches`. Projected rows are what the table would say if the
  matches in progress went the way the strength says they go; official rows only move when a
  match is finished.

**The calendar is the season, and it is drawn once.** `GET /season/{id}/calendar?build=true`
draws 22 matchdays, a championship window in each of the three divisions and a cup window in
the second slot. `MatchDay`, `Round.CompletedAt` and `WindowsPerMatchDay` are what energy
recovery is measured against, so a squad's rest is a fact about the calendar rather than a
number a client invents.

**A cup cannot be drawn all at once, because nobody knows who is in the quarter-finals before
the round of 16 is played.** So the calendar draws the first round and nothing else, and
`CupProgressionService` draws each round after the one before it is decided. Three rules:

- **The cup advances on the finish of a match, not on a timer.** A bracket that advances on a
  clock is a bracket that can be ahead of the football: the quarter-finals would be drawn
  before the last second leg of the round of 16 had been played. The moment the last leg of a
  round is over, the round is complete and the next one exists in the same breath.
- **The cup commits its own writes before it reads the round back.** The round is read
  untracked, so a tie decided only in memory reads back as undecided. A service that asked
  first and saved afterwards would see the last tie of the round as still being played, decide
  the round was unfinished, and never draw another one — a cup that stops after two matchdays
  with no error anywhere to explain why.
- **The aggregate is added by club, not by side.** The two legs swap ends, so a sum taken by
  side credits a club with a goal it did not score and can send the winner out of the cup.

A level tie goes straight to penalties — there is no extra time in a cup tie — and the taker is
the engine's own choice from the eleven that played, measured by `MatchEngine.PenaltyConversion`
against the keeper in the other goal. `CupTie` records the winner *and* the loser, because the
losing side of a final is a fact in its own right: it is the runner-up and it goes on the shelf.
Trophies are written, not recomputed, so a club that is relegated after winning still won.

## The Match Model

Everything below is a rule, not a setting, and the numbers that balance it live in one
place — `Domain/Matches/MatchRules.cs` — rather than in the loops that apply them. A rule
whose constant is buried in the middle of an action table is a rule nobody can tune.

**A tick is half a minute.** `MatchRules.SecondsPerTick = 30`. At one tick per minute a
goal, a card and a substitution all shared the same instant. Most ticks are quiet:
`ActionChancePerTick` is the share of them in which anything happens at all, and the rest
is most of a football match.

**The clock stops for a moment after something that needs looking at.**
`TicksHeldAfterMajorEvent = 3` — a goal, a red card, a serious injury, a substitution. The
hold is counted in *ticks*, not in seconds of wall clock, so it is the same stretch of the
match at 1x and at 4x.

**A team is measured unit by unit.** `TeamStrength` reads the eleven that is actually on
the pitch and returns three numbers — attack, midfield, defense — each built from the
attributes of that unit, weighted by the energy the men have left, then scaled by the
shape. A missing player is not one less body, it is a hole in one of the three numbers, so
losing a striker and losing a centre back do not limp in the same way.

**The shape is read off the eleven, not chosen.** `Formation.FromComposition` counts the
lines on the pitch and `Formation.FromSquadComposition` reads the shape of a club off the
players it has to pick from, scaled to ten. There is no formation setting anywhere: a
manager picks eleven men and the shape is a consequence, which is also why replacing a
striker for a midfielder changes the shape the engine is measuring. The engine
recomputes it after every substitution, and the auto-selected eleven
(`MatchService.SelectAutomaticEleven`) fills the shape the club is made of rather than the
eleven best men by one flat rating.

**A tactic chooses the men; it does not relabel the shape.** `Tactics` holds ten shapes
(`442`, `433`, `4231`, `541`, `532`, `352`, `343`, `451`, `4141`, `334`), each a list of
lines rather than the string a manager says, and `Tactic.IsComplete` is what keeps the
catalogue honest — every one of them is a whole eleven with one keeper. Naming one decides
which men are picked for which line; it does not become the formation the engine measures,
because that still comes off the eleven that actually turned out. The two are separate on
purpose: a manager who hand-picks something the catalogue has no name for is measured on
what he did, and a manager who orders 4-2-3-1 and gets 4-5-1 out of his squad is told so by
the formation, not by a label he set.

**A line is filled by the men who are best at that line's job.**
`PlayerMetric.TacticalMetric` blends the general metric with a reading of the player as the
man who holds or the man who creates (`LineJobWeight`, 0.55, in `MatchRules`). The weight
has to be a majority: a creative midfielder is a handful of points ahead of a holding one
on the general metric, and a minority weight loses that every time, which is the shape
becoming a label again. The other 45% is what keeps a tactic from becoming a filter — the
man who is simply the best footballer available still starts whichever line he is told to
play in. The profile picks *which* rating (`HoldingRating` or `CreatingRating`) rather than
adjusting one number, because a uniform nudge scores a holding and a creative midfielder
identically and the two lines are then filled by the same men.

**The eleven the staff suggest is the one the engine would have picked.**
`GET /match/squad-suggestion` answers with `SelectAutomaticEleven` for a club and a shape,
because the lineup screen used to work the eleven out itself, by its own idea of who was
good — and having it in two places was wrong twice over: the client could propose a reserve
goalkeeper, whose attributes are high by design, and it handed out two places per line
whatever the club was made of, so the eleven it proposed had no shape. The screen is where
a manager reads the suggestion, not where it is worked out. The staff weigh energy, because
`Metric` already carries it: a tired player drops in the order but never disappears from it,
so a manager who has to choose still sees the better footballer *and* sees how tired he is.

**A round is told back in the words the match used.** `GET /match/round-report/{roundId}`
composes the league panel from the persisted events, and the summary of a match is the
goals' own narration read out of `match_events.payload.description`, in sequence order. It
is not written again at report time and there is no random draw in the method: the words a
match uses are part of its identity, so a report that re-rolled them would give one fixture
two accounts of the same afternoon. A goalless match has no goal to quote, and saying that
nobody found the net is the truest thing there is to say about it.

**A goal says "GOL!", an own goal says "GOL DO".** `GOL DO {clube}` is the own-goal bank's
wording, where it names the club that *benefited* from a defender's mistake. `Goal` and
`GoalRebound` used it too, which printed the words of an own goal over the name of the man
who had just scored — the feed read as the exact opposite of what happened. The phrase is
now reserved, and a test does not lock it: reworded lines are supposed to break nothing.

**Initiative comes from the two sides, not from a coin.** The side on the ball is chosen by
`midfield + attack * 0.65` compared with the other, so a side that controls the game plays
more of it and a side with better forwards converts more of what it does.

**Possession is a fact about the match.** `MatchState` accumulates
`HomePossessionSeconds`/`AwayPossessionSeconds` — the seconds each side actually had the
ball — and the percentage on the screen is read off those and nothing else, clamped to
30–70. It used to be a counter that drifted by a random amount per action, which is why it
could sit at 96%.

**Running costs energy, and the energy is what the strength is made of.** Every tick
drains each player on the pitch by `0.035` times his age factor and any injury he is
playing through, doubled one tick in fifteen, with a floor of 35: nobody plays a match on
empty. Because `TeamStrength` multiplies energy in, a tiring side gets measurably weaker as
the game goes on, and because the injury rate rises with fatigue, the tired are also the
ones who go off.

**Recovery between matches is asymmetric, and that is the whole reason a squad is
rotated.** A player who played gets `3..7`; a player who rested gets `11..18`. The bands
do not overlap. It is applied at the end of the match from the match's own seed, so a
replayed match recovers exactly the same way.

**A knock is three questions.** How bad it is (from age and fatigue, and 1.9x more likely
for a player already carrying one), whether it ends the match (a light knock is a player who
stays on and is more vulnerable; a grave one is a player who leaves), and for how long (the
engine draws 2–4 matches when it happens, and only a knock that took him off becomes a
season absence).

**A club is never left without somebody in goal.** Losing the last goalkeeper reaches for
the reserve keeper on the bench first, whatever the manager would have preferred, and only
promotes an outfielder when the bench has nobody. That is not a discretionary change: a
player who cannot continue is replaced, and the eleven cards under the scoreboard always
have a keeper in them.

**The engine plays the club nobody is watching.** The manager's club is his to change; the
engine substitutes for the other side, or for both when nobody is watching, with three
triggers: a correction or a change for wear in the window after the interval, a booked
player on low energy, and a spent man late on. A replacement comes from the same line as
the man coming off — a club that ends a match 2-7-1 has not been managed, and the shape is
what the rest of the match is measured against. The engine and the manager share one
`MatchRules.MaxSubstitutions`, so the opposition can never get a sixth change the manager
is not allowed to make.

**The swap rules live in one place.** `MatchSubstitution` is used by `MatchService` for a
manager's command and by the engine for its own, so what a manager may do by hand and what
the engine does on its own cannot drift apart.

**A substitute is spent.** A player who has left the pitch carries `SubbedOff`, and naming
him again is refused with `PlayerAlreadySubstituted`. The bench is not a queue of men
waiting to play: it also holds the substitutes who have already been taken off. Without
this the engine takes a defender off, puts a midfielder on, and brings the defender straight
back — the same change twice, and a second one nobody asked for. `MatchSubstitution.CanSwap`
refuses it for the engine and `MatchService` refuses it for the manager, and the
substitution screen greys him out rather than offering a change the backend will reject.

**Added time is announced while there is still time to play it.** The total is drawn once
at kick-off, split between the halves (40/60, because the first half collects injuries and
the second collects goals), and each half announces its own minutes as
`StoppageTimeAdded` at 41' and at 86' — while the half is still being played. A referee who
waits for the whistle has told the manager something that has already happened.

**An own goal is an error, and it comes from a defender's error.** It is a defensive
clearance that goes through the wrong net, never a corner finding the net: a corner is only
a ball arriving in a dangerous place and needs nobody to be wrong. `OwnGoalFromClearance` is
per defensive action, which is roughly a tenth of an own goal a match — a thing that happens
twice a season is a story, and a thing that happens twice a match is a rule nobody argued
about. `MatchGoals` and `MatchOwnGoals` are kept apart and add up to the number of balls
that went in, so an own goal is never also counted as a goal of his.

**A match is narrated, not only scored.** `BuildUp` is the ball being played — a pass
completed, a duel won, a ball intercepted — and it is most of a football match. The
prototype only had fouls at one action in seventeen, which produced a match with no fouls
in it and a penalty once every ten matches; a match with no fouls in it has no cards, no
penalties and no eleven metres. The bands in `MatchRules` are football-shaped, and
`AMatchPlayedOutLooksLikeAFootballMatchAndNotLikeASpreadsheet` is the guard on that: over
60 matches it holds roughly 1–3.5 goals, 8–28 shots, 2–11 fouls, 0.5–6 cards, 0.5–6
corners and 1–8 changes.

**The eleven is said before the whistle, and the interval is said at the second half.**
`LineupAnnounced` is emitted for both clubs before `KickOff`, with the eleven grouped by the
line each man plays in — a manager picks those names, and a match that starts without saying
who is playing hides the only decision he made before anyone watched a minute of it. The
second half opens by saying whether either manager changed anything, and if so who for whom;
`MatchState.FirstHalfChanges` exists for that, because "o time fez uma troca" is not the same
information as the name of the man who came on.

**The feed says it more than one way.** `MatchNarration` holds several ways of saying each
beat and draws one of them per beat, from the match's own random source so a replayed match
says the same things in the same order. Formatting one sentence per event produced a feed
that read like a spreadsheet with a pulse — the same two constructions a hundred times, in
the same voice — and a manager who watches ninety minutes of that stops reading it. The
draw from the same source also means the words a match uses are part of its identity, so
`MatchEngine_FullMatch_IsDeterministic_WithSameSeed` covers them along with everything else.

Two rules the code depends on:

- A narration bank has a fixed arity: every line in a bank takes the same arguments, and a
  line may use fewer of them than it is given but never more. `Say` composes with
  `string.Format`, so a bank line with a placeholder the call site does not pass throws
  mid-match, and a match that throws halfway through is a match nobody saw the end of.
- The narration is written in Portuguese, with the accents. A feed written in ASCII does not
  look like a bug in a game — it looks like a game that cannot spell.
- A test never asks what the narration said. It asks what the engine did: `GoalScored` means
  a goal, `SubstitutionMade` means a change. Two tests read the prose and both broke the day
  a line was reworded, which is the whole cost of a bank that is supposed to be reworded.
- A number on a card is the number for that screen. `goals` on a profile is the season and
  `matchGoals` on the eleven under the scoreboard is this match; a striker with nine for the
  year who has not scored today has scored nothing on that card, and `matchOwnGoals` is the
  red ball next to it.
- **A command publishes its events once, whoever issues it.** `MatchCommandPublisher` is the
  seam: the hub calls it for a command that arrived over the hub and `MatchController` for
  one that arrived over REST, so a manager's own substitution is narrated to every follower
  of that match exactly like an event the engine produced. The client dedupes by
  `Match.Sequence`, so being told twice is harmless and being told never is not.
- Enums must serialize as **names** on both transports. MVC needs
  `AddControllers().AddJsonOptions(...)` and SignalR needs
  `AddSignalR().AddJsonProtocol(...)`; setting only one leaves the other emitting
  numbers while the frontend compares against names.
- The match row is rewritten on a cadence (every 5 minutes plus any goal, card,
  substitution or half-time), but **events are committed on every tick**. Each tick
  runs in its own DI scope, so anything left unsaved is discarded.
- **A converted penalty also emits `GoalScored`.** `PenaltyTaken` says the kick was
  taken and is emitted for a miss as well, so it cannot stand for a goal on its own.
  Anything that reacts to goals — the feed, the scorers, the sound — listens to
  `GoalScored`, and a penalty goal has to reach it.
- **A shot on target with nobody in goal is not a goal.** `ResolveShot` returns before the
  roll when the defending club has no keeper, because an empty net is what a club without a
  goalkeeper is punished for. Since `EnsureGoalkeeper` guarantees there is always one, the
  arm is only reached by a club that has run out of men entirely.
- **The finished match answers from the statistics row**, not from zeros, and
  `match_statistics` carries the two formations so a results screen cannot report 4-3-3 for
  a match that was played 4-4-2.

## Faces

A player has a face, it is drawn once, and it is the same face every time:

- **The face is part of the identity, not of a season.** It lives on `players.face`, beside
  the birth date, for the same reason the birth date is there: the same man is recognised in
  every edition, and a face that changed on transfer would be a different man.
- **A face is stored, never redrawn.** `players.face` is a `jsonb` column holding the raw
  `FaceConfig` of [faces.js](https://github.com/zengm-games/facesjs). A profile screen that
  generated one on the spot would show a manager a different man every time he opened the
  card, and recognition is the whole point of a face.
- **The contract is the library's, on both sides.** C# carries the face as a `string?` and
  TypeScript as a JSON string, because the shape belongs to the library that draws it and a
  C# class mirroring `FaceConfig` would be a second copy of it to keep in step. The only
  thing that reads it on the client is `parseFace` in `components/Common/PlayerFace.tsx`.
- **The pool is drawn in JavaScript and embedded.** faces.js is a JavaScript library, so
  `npm run faces:generate` (in the frontend) draws the pool and writes it to
  `backend/NinjaEleven.Infrastructure/Resources/faces.json` as an embedded resource, read by
  `FaceCatalog`. The draw is seeded, so running it twice produces the same pool.
- **No face is a real state, and it is `null`.** `''` is not a JSON document, so a `jsonb`
  column cannot hold it: a player who was never drawn has `NULL`, and `PlayerFace` renders
  nothing at all rather than a placeholder. A profile screen must survive a manager without a
  face.
- **A face is dealt, not drawn, per player.** A club of twenty-three men with two of them
  sharing a nose reads as a copy-paste, so a new world and a backfill both shuffle the pool
  and deal from the front. The pool is larger than a squad for exactly this.
- **Resuming a seeded world still has work to do.** The seeder skips a world that already has
  teams, so a pool that arrives later would leave everybody faceless; `DatabaseSeeder` gives a
  face to every player whose `face` is null before it returns.

## Sound

A goal that happens on screen and makes no sound is a broken game, so the audio rules are
about never losing one:

- **The play is always attempted, even after a refusal.** A match screen reached by a
  refresh has no user gesture behind it; the first sound is refused, and an engine that
  believed it was locked afterwards only ever *queued* the rest — so every goal for the rest
  of the match was silent. Asking costs nothing when the answer is still no, and it is the
  only way the engine notices the page has become allowed to play.
- **The backlog is short.** A goal the manager missed is worth hearing on the next click; a
  goal from the first half is not. Three, then the oldest is dropped.
- **A sound that throws does not take the batch with it.** Rewinding an element whose data
  has not arrived is guarded, and the hook reacts to each event inside its own `try`.
- **The opening batch is history, and the priming follows the match id.** The feed lives in
  one store shared by every screen, so a manager arriving from another match meets a log
  full of somebody else's events.

## Sound

The match is heard, never simulated: the files live in `frontend/ninja-eleven-web/public/audio/`
and Vite serves them at `/audio/*.mp3` in development and copies them into the build. One
engine (`services/audioEngine.ts`) owns every element, and `hooks/useMatchAudio.ts` binds it
to the event stream.

| Sound | When |
| --- | --- |
| `whistle.mp3` | `KickOff`, `HalfTimeReached`, `SecondHalfStarted`, `MatchFinished` and `Foul` |
| `goal.mp3`, `goal1..4.mp3` | `GoalScored` and `OwnGoalScored`, drawn at random so no two goals sound alike |
| `crowd.mp3` | Looping under the whole match, from the first whistle to the last |

Two rules the sound depends on:

- **Only events that arrive while the screen is open are played.** The event log is loaded
  whole when the match screen mounts, and a manager who joins at minute 60 must not hear
  the whistle of kick-off, so the opening batch is history and only later sequences sound.
- **A blocked play waits for a gesture, it is not dropped.** Browsers refuse audio until
  the page has been interacted with, and there is no way to ask. The engine plays and only
  queues on refusal, replaying whatever was held back on the first click.


## Frontend Architecture

- **pages/**: Screen components (StartScreen, LeagueScreen, LineupScreen, MatchScreen, TeamViewScreen)
- **components/**: Reusable sub-components (Common/, League/, Match/, Modals/)
- **api/**: REST API client (axios-based)
- **signalr/**: SignalR client for real-time match updates
- **state/**: Zustand store for global frontend state, and the `ProfileProvider` that owns
  the player and club overlays
- **hooks/**: Hooks that bind the backend to the screen (`useMatchAudio`)
- **services/**: Utility formatters and the sound engine
- **types/**: TypeScript interfaces matching C# DTOs

## Key Rules

1. **Backend is the authority** — frontend never computes match results or standings
2. **DTOs are the contract** — TypeScript types must match C# response DTOs
3. **Single source of truth for match state** — backend MatchEngine
4. **Frontend only presents** — animations, transitions, and visual state are presentation-only
5. **A number on a screen is a number the engine decided** — the shape strip, the energy
   bar, the possession split and the ball-carrier highlight are all read from the DTO and
   recomputed nowhere in the client. The one thing the frontend adds is the class a value
   maps to; the value itself always arrives from the backend.
6. **A name is always a door** — a player or a club seen anywhere leads to that profile.
   `PlayerName` and `ClubName` in `components/Common/Names.tsx` are the only way a name is
   made clickable, and `ProfileProvider` (in `state/`) is the only way a profile is opened,
   so no screen can be the one where a name is not a link. It renders as the text around it
   until the pointer goes over, so a screen of names does not become a screen of buttons.
   Somebody else's club is a modal; the manager's own club is the screen the game starts on.

## Reading a Player

`GET /player/{id}/profile?seasonId=` answers a player whole, in one call, and the season is
optional because "how has this striker done" is a question about more than one season. The
profile is assembled by `PlayerService` and not by the screen: two screens asking "how many
goals" must not be free to answer differently.

- **The season's totals and the career's are the same lines filtered two ways.** They are
  summed from the player's `match_player_statistics` rows, and the season column is the
  career filtered by `SeasonId`. The season state carries a goals counter of its own and it
  is deliberately *not* what is shown — a profile that printed the counter in one place and
  the sum of the history in another would be two career numbers, and the manager could not
  tell which to believe.
- **A player's history comes from the lines, and a line means he played.** A man on the
  bench who was never used gets no line at all: an unused substitute did not appear, and a
  history that counted him would be counting a man in a suit.
- **"14 (3)" is made of two numbers** — games started and games entered off the bench. They
  are kept apart because a single appearance count throws away the only thing a manager
  really wants from it: whether the staff trusted this man to start.
- **A name inside a sentence of the feed is a link, and the sentence is not rewritten.**
  `buildNameIndex` collects the two clubs and the twenty-two men plus the bench of *this*
  match, and `NarrativeText` claims each name in one pass, longest first. A name two people
  share is left as plain text on purpose: there is no honest way to say which one the
  sentence means, so it does not become a link that opens the wrong profile half the time.
