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
GET  /team/{teamId}/standing?seasonId=

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
                                          GET  /match/live/{teamId}

GET  /season                                POST /league/setup
GET  /season/{id}                           GET  /league/standing/{competitionSeasonId}
GET  /season/current                        GET  /league/standing/{competitionSeasonId}/team/{teamId}
POST /season                                GET  /league/scorer/{seasonId}?topN=
GET  /player/{id}/profile?seasonId=

GET  /team/{teamId}/scorer?seasonId=&competition=&topN=

GET  /team/{teamId}/profile?seasonId=

GET  /competition/{editionId}/top-scorer-prize

GET  /world/due?wave=          POST /world/advance?teamId=
POST /world/play-due?wave=&teamId=

PUT  /team/{id}/name                          PUT  /team/{id}/crest
PUT  /team/{id}/colors                        PUT  /team/{id}/kits
```

Domain errors return RFC 7807 with a stable `code` (e.g. `TeamNotFound`,
`SeasonRequired`, `NotEnoughTeams`); the frontend never parses messages to learn rules.
Malformed requests (an id that is not a guid, a missing field) answer 400 with
`code: ValidationFailed`. `POST /league/setup` is idempotent: when the edition already
has a schedule it returns the existing one instead of creating a second one.

**A read is a read, not a question per row.** A service answers about a set — a division, a
matchday, the whole world — by reading the set and then working it out, and never by asking
the repository once per item. That is not a style preference: `GET /ranking` used to ask for
each club's own history, and each club's own history was three seasons of tables, and each
table read a squad man by man, so a page of sixty-four clubs was a quarter of a million round
trips and answered in half a minute. The same rule is what keeps `GET /fixture`,
`GET /league/standing/{id}` and `GET /team/{id}/standing` under a tenth of a second.

**Is this club playing right now? is asked of the registry, not of the fixtures.** A club
plays at most one match at a time, so `GET /match/live/{teamId}` is a walk of the matches
being played — a handful on a matchday, none at all between them — rather than a read of
every fixture of the season to find one that happens to be in progress. The registry holds
the live working memory and nothing else, so a match that is not in it is not being played,
whatever the persisted row still says. The answer carries the score, the minute and the
interval, because a navigation badge that says "ao vivo" and nothing else is a badge the
manager has to click to learn whether the game is worth leaving the screen for.

The shape of the fix is always the same: a reader that takes a set (`GetSquadsAsync`,
`GetPlayersAsync`, `ListSeasonViewsAsync(seasonIds)`, `ListByCompetitionSeasonsAsync`,
`ListByCompetitionSeasonIdsAsync`, `ListSeasonStatesByPlayerIdsAsync`,
`ListByIdsAsync`, `ListByFixtureIdsAsync`) and a service that calls it once. A loop that
awaits a repository call inside it needs a reader that takes the whole set, and the reader
belongs in the repository interface rather than being hidden behind a service that re-queries.

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

Brazil is not one league, and the code stopped pretending otherwise in four divisions of
sixteen. Three things follow from that, and they are the three things that were got wrong first:

- **A club has no division. An edition has a division.** `Division` is permanent and a club
  belongs to one for a season through `competition_participants`, so a club that is relegated
  is the same club next season in a different division. A `divisionId` on `teams` would make
  the move delete the club.
- **A competition is not an edition.** `POST /league/setup` used to take a list of clubs and
  a season, which is a request that cannot say which of the four divisions it means. The
  client asks `GET /competition/by-season/{id}` for the editions, picks one, and reads its
  clubs from `GET /competition/{editionId}/club`. A client that joins a club list to a
  division list itself is how a club ends up in the 3ª Divisão.
- **A table is official or projected, and the backend says which.** `GET /league/standing/{id}`
  returns both, plus `hasLiveMatches`. Projected rows are what the table would say if the
  matches in progress went the way the strength says they go; official rows only move when a
  match is finished.

**The calendar is the season, and it is drawn once.** `GET /season/{id}/calendar?build=true`
draws 34 matchdays of a 64-club world and puts the football of the whole country on them:
a championship round in each of the four divisions every day from day 2 to day 31 — round `r`
is day `r + 1`, so the calendar is one fixture list per day rather than eight — and the cup in
waves on the days around them (32-avos on 7 and 8, 16-avos on 12 and 13, the round of 16 on 17
and 18, the quarter-finals on 22 and 23, the semi-finals on 27 and 28 and the final on 32 and
33). Day 34 is the rest day and the day the next season opens on. `MatchDay`,
`Round.CompletedAt` and `WindowsPerMatchDay` are what energy recovery is measured against, so
a squad's rest is a fact about the calendar rather than a number a client invents.

**A day has two windows, and the round is in the first one.** `CompetitionRules` holds the
hours — 15:00 UTC for a championship window and the Supercup, 21:00 for a cup leg — so the
seventh day of a season carries the sixth round of the divisions and the first legs of the
cup, and they are two windows six hours apart rather than one day of football. When a day
carries both, the league is the first wave and the cup the second: a cup leg played by a side
that has not yet played its league game is a leg played by a club that is not there yet.

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

## Moving the World

`NinjaEleven.Scheduler` is a .NET Worker Service on Quartz. Its jobs are thin — they ask
`CompetitionExecutionService` to play one kind of window, and that is all a job is. No job
knows what a goal is, how a round robin is drawn or when the artilharia is paid; a job that
called a match service directly would be one rule away from being a second opinion about
football.

**A hand walks the world, one window at a time.** `POST /world/advance` calls
`CompetitionExecutionService.AdvanceTheWorldAsync`, which finds the earliest matchday that is
due and still has an unplayed window in it, plays that single window and answers with what it
did: `WorldAdvanceKind.Window`, or `SeasonClosed` when the window it played was the last
football of a season, or `Nothing` when the world owes nothing. A world that owes five hundred
windows is five hundred presses, not one call and not a thousand — so the seasons before this
one (`World:MatchDayDuration`, `World:CompressedSeasonAnchor`, `SeasonSchedule`) are gone, and
so is the cron's job of deciding what a day is worth. The scheduler still exists and still
calls the same service on its three crons, for a world someone wants running without a hand on
it; it is a caller of the world's rules and not their author.

PostgreSQL is the only coordination. There is no Redis, no lock table and no message broker,
because a claim that is a row and a `SELECT ... FOR UPDATE` is the whole answer to two
schedulers firing in the same minute. `RoundExecutionStoreTests` is that claim against a real
PostgreSQL — an in-memory provider has no rows to lock and would answer "yes" to both.

- **A window is claimed before a single fixture is touched, and completed only when every
  fixture of it is finished.** A window left half played is not restarted: the fixtures that
  were played are already `Finished`, and the run that comes back finishes the ones that are
  left. A fixture that throws is reported and stepped over, and it takes only itself down.
- **The claim carries a lease *and* an owner.** The lease is what makes a crashed process
  recoverable; the owner is what stops the process that lost the claim from closing the window
  over the one that took it. A window that recorded only *when* it was claimed cannot tell a
  retaken claim from a young one, so the loser would still look like a valid owner for the
  length of the new lease and would write "completed" above fixtures still to be played.
- **A released window is nobody's again.** A window that failed goes back rather than being
  held, so the next run finishes it instead of the next run after the lease expires. And the
  release says `Scheduled`, because a row that still says "running" is a row that says
  somebody is playing a window that nobody is playing.
- **A window closed by hand is never offered to the scheduler.** `Round.HasBeenExecuted`
  answers from either column — the claim or `CompletedAt` — because a window a manager finished
  by pressing a button has a completion and no claim.
- **Only a match that reached the final whistle decides its fixture.** `Match.IsFinished` is
  also true of an *abandoned* match, which is precisely a match that did not get there, so the
  reconciliation pass reads `MatchStatus.Finished` and not `IsFinished`. Reading it the other
  way closed a window over football that was never played: the fixture was marked finished
  above an abandoned match, the window counted itself complete, and the calendar carried on
  with a matchday that had no match in it. An abandoned match leaves its fixture owed.
- **A window that has begun is not begun again.** A match that finishes asks the day whether
  there is anything left to kick off, and the fixtures the world has not walked to yet are
  still on the schedule — so that answer used to be yes, and each of them was started twice:
  once by the matchday and once by the walk that arrived afterwards, which abandoned the
  first. `MatchdayService.AdvanceAsync` therefore starts a wave only when nothing of it has
  been kicked off, which is what makes it the opening of a window rather than a second
  opinion about one that is already running.
- **A match already on the pitch is left to whoever put it there.** The cup window opens
  every fixture of it at once, so the walk that plays it arrives to find the day already
  playing; taking those matches over abandoned and restarted them under whoever was watching.
  `PlayFixtureAsync` reads a fixture's own state and reports `PlayedElsewhere` instead, which
  is also the answer that keeps the window owed.
- **The manager's own fixture is started and left, not simulated.** A window that holds a
  match of a club somebody is in charge of opens that fixture — a real kick-off, a real
  session — and stops, reporting it as `LeftForTheManager`. The window is owed until he
  finishes it, and the run that comes back reconciles it from the match he played. A world
  that simulates the game its manager is waiting to watch is a game he never played. The club
  is read from the world (`IManagedClubReader`: the managers with a user behind them) and not
  from the request, because the scheduler walking the same season is walking it for the same
  man; a club the caller names is added to that answer rather than replacing it.
- **A match left on the touchline and never touched is nobody's match.** The world waits for
  the manager, and a world that waited for ever would say so by playing nothing at all — the
  same window, the same fixture, the same hundredth of a second, over and over, which is
  indistinguishable from a broken route. So `MatchService.ReleaseTheUntouchedMatchAsync`
  asks the match rather than a clock: minute zero, unfinished, and either no session here or
  one still marked `LeftForTheManager`. A match the loop is driving and a match the manager
  has claimed are both somebody's, and are refused.
- **One match, one driver, and the driver is named.** `LiveMatch.Driver` says whether the
  headless walk is on it (`WalkedByTheWorld`), whether it is waiting on the touchline for the
  manager (`LeftForTheManager`), or whether the background loop may have it (`None`). The
  loop moves a clock that is nobody else's, the walk stops the moment it is not the driver
  any more, and `AttachManager` hands the clock back — a match left for a manager and then
  claimed is a match the manager is watching, and a loop that kept off it would show him a
  game that never moves.
- **A matchday that is behind is owed, not skipped.** The calendar is read from its beginning
  up to today, never from yesterday: a bound of "yesterday" caps how far behind the world may
  fall at the price of never playing anything again, and it fails silently, reporting nothing
  due over a world with hundreds of fixtures outstanding. The tolerance in
  `WorldExecutionOptions` reaches backwards as well as forwards, so a process that woke up
  twenty minutes early does not sleep through its own window.
- **A match belongs to the process that kicked it off.** `Match.SessionHost` and
  `SessionHeartbeatAt` say whose working memory a row is in, and a lease says how long that
  claim is honoured. Without them a restart abandons somebody else's football; with them only,
  a process killed at minute sixty strands a fixture and a round that can never close. A
  process reclaiming its *own* match does not wait out the lease — its memory is gone, and
  waiting would add minutes of nothing to every restart.
- **A match this process could not finish is given up on, not left running.** Three exits in
  the headless walk can stop a match before the final whistle, and each of them used to return
  with the match still on the pitch and its session still in this process's registry — so the
  next run of the window was told the match was already being played, for ever, by a driver
  that had stopped. `MatchService.AbandonAsync` closes the match as `Abandoned` and reopens
  its fixture, so a window is retried rather than stuck. It is `Abandoned` and not `Finished`
  because the match did not reach full time, and a row recording a score that was never played
  is a lie in the results table.
- **A row read untracked is written through the instance the context already holds.** Reads
  here are `AsNoTracking`, so a command holding a match holds a copy the context does not own,
  and anything on the command's path that attaches one leaves the context with a second
  instance of the same row. EF refuses to attach the second, and it refuses from inside a tick
  — an "already being tracked" error raised by a command that only ever touched one match.
  `MatchRepository.Update` and `FixtureRepository.Update` copy the values onto the tracked
  instance instead, which is the one that will be written.
- **SignalR observes; it does not decide.** A client following a match the scheduler is
  playing is sent that match's persisted events in the engine's own event shape, so a manager
  cannot tell which process is playing his football. A match is simulated from the whistle to
  the final whistle in its own DI scope per fixture, so one match cannot be advanced by two
  callers and nothing a client does can reach into a match it does not manage.

## The Balance Laboratory

`tools/NinjaEleven.BalanceLab` is an executable that references nothing but
`NinjaEleven.Domain` and asks the model questions a match cannot answer: how often does this
attacker beat that defender, given these two amounts of energy. It changes no production
file, and it is measured rather than trusted.

- **A balance number is worth nothing if the laboratory has drifted from the engine.**
  `LabStrength` is `TeamStrength` with one thing changed — the ramp is a parameter instead of
  a hardcoded `Energy / 100` — and `BalanceInvariantTests.TheLaboratoryStillMeasuresTheEngine`
  is the assertion that holds it there. Every other number the laboratory prints is
  downstream of that one.
- **A duel is not a match.** `DuelSimulator` rolls one chance a hundred thousand times
  instead of a match two hundred times, with injuries and the clock out of the way. It
  measures the thing the curve touches rather than the whole evening around it.
- **The invariant is the hierarchy, and it is a ratio.** "Energy may not cancel a quality
  gap" is `InitiativeRatio(90, tired, 45, fresh) > 1`, and it is the assertion the design is
  judged by. The production ramp fails it: a linear ramp lets a man on 20 deliver a fifth of
  himself, which cancels a striker who is twice as good as his marker.
- **A formula that is bolted carries no information, and the audit says which are.**
  `ScaleAudit` sweeps every action formula over the 1..100 scale and reports the band
  between its own clamps — the band in which two different players are still two different
  players. Anything outside it is a constant wearing a formula's clothes.

**The attributes are on 1..100 and the action formulas were written for 1..20.** That is
the finding under everything else here: `SkillFactor` lives on 7–19, the on-target chance
on 1–2, `ResolveShot` on 47–77. `TeamStrength` is the only formula that reads a raw 1..100
attribute without saturating — and it is precisely the one energy multiplies inside. Energy
does not dominate the engine because it is too strong; it dominates because quality is dead
everywhere else, and a tax that is the only live path for a difference looks like a veto.

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

**A manager commands his own club and no other.** `MatchState.ManagerCommandsTeam` is the
one test, and it guards the substitution and the penalty taker: a client sends the team id
it is acting for, and a manager follows a whole matchday, so a command naming the opposition
is one a screen can send without meaning to. A match with no manager attached — a headless
one, simulated from the calendar — has no author for a command and refuses every club. The
engine's own changes do not come through here, so this refuses a *command* and not football:
the match still runs to full time with the engine choosing for the side nobody is watching.

**A substitution moves nobody's energy.** Energy is what the ninety minutes cost, and the
ninety minutes are the ticks: `MatchEngine.DrainEnergy` is the only thing in a match that
lowers it. A change of shape is not a tick, so the two men who swap places arrive and leave
with the numbers they had, and the man who comes on does not appear fitter than the eleven he
is joining. `ASubstitutionMovesNobodyEnergy` reads all four lists before and after a swap and
holds every one of them still; without it a change is the one moment in a match where a
player is suddenly not the man he was a second ago.

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

## The Club's Page

`GET /team/{teamId}/profile?seasonId=` answers a club whole: who it is, who runs it, how many
men it keeps, what it has in the bank, what it has won and everything that has ever happened to
it. It is one call because the page is one thing — a screen that asked for the balance here, the
roster there and the rest of the club from a stand-in it drew itself was a page that could show a
club's size beside a club's balance from two different years and a history that was invented.
The stand-in was *stable* per club, which is what made it worse: it looked correct forever and
was wrong from the first season.

- **A club's history is two kinds of fact, and they are kept apart.** The founding, the moves
  up and down the pyramid, the titles and the artilharias are **derived** by
  `ClubProfileService` from the competitions themselves — a promotion is two editions of the
  pyramid compared, and a title is a row that already exists. A rename, a crest and a change of
  colours are **recorded**, because nothing anywhere can work them out: they are decisions, and
  `club_events` is written in the same unit of work as the decision itself
  (`TeamService.UpdateNameAsync`, `UpdateColorsAsync`, `UpdateCrestAsync`). A page that derived
  them could only guess, and a guess that looks like a memory is the one kind of lie a club's
  page cannot carry.
- **An event carries the season it belongs to, or none.** A manager renames a club in the
  winter quite often, and dating that decision by a football season that had nothing to do with it
  would be a small invention on the one page a manager reads to find out what actually happened to
  his club. So `seasonId`/`seasonNumber`/`seasonName` are nullable, and the screen says "—" rather
  than borrowing the nearest season.
- **An event's id is derived when there is no row behind it.** A promotion has no guid, so its id
  is built from what it was derived from. It is stable anyway — which is all an id has to be for a
  list of lines to hold on to across two reads.
- **A club nobody manages has no manager, and the endpoint says so with an empty string.** There
  is no coach entity in the world; the profile reads the manager row if there is one. An empty
  name is not a value to fill in with a stand-in, and the screen offers no rename control for a
  club whose manager he is not — a control that can only ever fail is not a control.
- **`seasonId` moves one number and not the rest.** It sizes the roster; the balance is
  deliberately *not* narrowed to a season (it is the club's money today, which is the number a
  manager has to pay a wage with) and the history is the club's whole career.
- **The trophies on the shelf name their division and carry its tier.** "Campeão" and "Campeão"
  are not the same trophy, and a shelf holding the 1ª and the 3ª has to draw two different cups.
  `divisionTier` exists so that decision does not have to be made by parsing the leading number out
  of a label.
- **A read is a read.** The service takes two set-readers —
  `IPlayerRepository.ListEditionScorerLinesForEditionsAsync` and
  `ITeamRepository.ListDivisionSeasonsAsync` — and asks each of them once, because a club's whole
  history read one season per season and one scorer line per striker is the old mistake at a
  smaller scale.

## The Shirt

A sponsor pays for exposure, so the price of a shirt is priced the way a crowd is priced. The
whole vocabulary is `Domain/Sponsors/SponsorPricing.cs` and `SponsorRules.cs`, and nothing in
the game prices a shirt outside them.

- **The price is a product of five facts and a base.** `BaseFeeForTier(tier) × WeightFactor ×
  PositionFactor × FormFactor × CupFactor × CrowdFactor × PortfolioFactor`, rounded to the
  hundred. The base carries the division and the size of the company; everything after it is
  the club asking for more or less than that base says, which is the only part a season moves.
- **A missing fact is neutral and not zero.** `ClubSponsorFacts` holds nulls, and a club that
  has not played a match has no position, no form and no crowd of its own. A club whose facts
  are all zero is a club with a terrible season, and a fee that could not tell them apart would
  price a season's first day like its last.
- **A crowd is not extrapolated.** `CrowdSpread` holds both ends flat, like the price of a seat:
  a sponsor does not pay six times the fee for a ground three times as full.
- **A brand does not put two shirts in one championship.** `WantsThisClub` refuses a company
  that already has a club in this division, and a company whose slate is full. Both are real
  reasons and both are reasons a human sponsor has.
- **The size of a company is one decision, not four.** `SponsorSize` carries the weight, the
  slate, the appeal floor and the highest division together, and `Sponsor.Create` takes it as
  one argument — a national brand seeded with a local club's appetite is a company nothing
  downstream can reason about.
- **A company's size is dealt out of its name.** `SponsorRules.SizeFor` hashes the name with
  `Domain/Common/StableHash` (FNV-1a, shared with `ClubIdentityDefaults`) and takes a third, so
  a freshly seeded world and a migrated one arrive at the same book, and the catalog is not
  sorted into three piles by hand. The migration spells the same hash out in SQL rather than
  using the database's `hashtext`, which would have been a different answer.
- **The shortlist is not drawn.** It is every company that would take the club, ordered by what
  it pays, capped at `CandidateOffers`. A list that redrew itself on every visit would be three
  different boards of three different prices for the same shirt, and a decision taken on one
  would not be a decision. Only the *length* is drawn, from the club's own seed, so a club that
  signs without looking is paid the price the list would have shown.
- **A client cannot name a price or a length.** `SponsorSignRequestDto` carries the company and
  nothing else: a fee a screen could fill in was a number the book had never agreed to, and a
  manager budgeting his season against it was budgeting against a lie. `A sign pays what the
  list quoted` holds the seam.

## The Club's Scorers

`GET /team/{teamId}/scorer` is the league's own list with the club's name on it, and the
three filters it answers are all decided by the backend:

- **The competition is a walk, not a column.** A match line knows its match, a match knows its
  fixture, a fixture knows its round, and only the round knows whether the tie was a division
  match, a cup tie or a Supercup. So `?competition=Cup` is a query that joins five tables
  rather than a filter, and a scorers table that could only answer for the championship would
  have a cup final's goals *missing* rather than zero — which a manager reads as a cup where
  nobody scored.
- **An own goal is never a goal.** `OwnGoals` is summed into its own column. It is a
  defender's error, and a table that added it would put a centre-back on the list for the
  mistakes he made.
- **`IsStillAtClub` is a contract, not a shirt.** A membership with no end date. A man who has
  left stays in the answer with the flag against him rather than being filtered out, because a
  club's all-time scorers is the one page that must never lose a name: a screen that showed
  only the men under contract would quietly rewrite the club's history every time a window
  opened. The flag is what the client's "Ainda no clube" box filters on, so the answer is the
  whole list and the question is asked on top of it.
- **The order is goals, then the fewest games for them, then the fewest cards, then the
  oldest.** Eight in ten and eight in twenty are not the same striker, and `GoalsPerAppearance`
  is worked out in the service for the same reason every other number is: a rate the client
  invents is a rate two clients may invent differently. A player who never appeared is given
  `null` and not zero. The name is what a level pair is *printed* in and is not a rule that
  outranks a season's football.
- **The whole chain is `TopScorerTable.Rank`, and it is the chain every scorers list in the
  game is settled on** — the club's own page, a division's artilharia and the cup's. A yellow
  card is worth one point and a red is worth three (`ScorerStanding`), because the count is
  about who was on the pitch most and a red is what takes a man off it.
- **A pair level on the whole chain shares its place, and the place below it is paid to
  nobody.** Two men level on goals, games, cards and age are both second; both take the second
  prize; the third prize goes unwarded. There is no fifth rule to break a tie nobody can break,
  and a coin, a name or the order the rows came out of the database would each pay two equally
  good strikers different money for saying the same thing. The row carries `TiedWith` so a
  screen can say "empatado" rather than invent an order.
- **The season-wide chart is counted from the match lines, not from the season's own counters.**
  The counters carry goals and nothing else, so an artilharia ordered without games and cards
  would be a table of the whole country ranked by different rules from the ones its prizes are
  paid on.

## The Prize of the Artilharia

`GET /competition/{editionId}/top-scorer-prize` is what one edition pays its top three scorers,
and who holds each place. It is asked of an **edition** and not of a season because a season's
championship is three editions with three tables and three purses.

- **It is a share of the champion's own prize: 10%, 5% and 3%** (`PrizeRules.TopScorerRate`,
  spent through `TopScorerShareOf`). The base is the title rather than the whole purse, so the
  pyramid scales: the first division's top striker is paid a share of a title worth millions,
  the third's a share of a much smaller one, and the two are read in the same words.
- **The base is the title of the competition the goals were scored in**, which is the whole
  rule for a cup: the cup's artilharia is a share of the cup's own champion's prize
  (`PrizeRules.CupChampionPrize`), so all three of its places are shares of one purse and a
  third-division forward who tops the cup's scoring is paid exactly what a first-division one
  is paid. Reading a cup striker's share out of his own club's division would price the same
  goals three different ways according to the shirt he happened to be wearing, and it would make
  a cup whose artilharia is worth something a rule about divisions wearing the words of a rule
  about a cup. An edition that is neither a division nor a cup has no title, so it has no
  artilharia to price, and its rows carry `amount: null` rather than a number the service made up.
- **The shares are new money, not a slice of anybody's cheque.** The title is the measure of
  the prize and nothing else: a club that wins a competition and whose striker wins its
  artilharia is paid twice, and a club can take two of the three prizes in the same table —
  two men scoring is two things the club did.
- **It is paid when the edition is over, not when the season is**
  (`ScorerPrizeService.PayAsync`, called from `MatchdayService` as each window closes). A window
  closing is the only moment the world knows a competition is finished — a division's last
  matchday, the cup's final going to penalties — and a season close pays out everything at once,
  which would leave a club waiting months for a cheque for a cup it had already won. So the
  first division finishing its season while the third is two matchdays short pays its own
  artilharia and the third's stays unwon, which is why it is per competition and not per season.
  The reference is `artilharia:{editionId}:{playerId}` — keyed by the player rather than the
  place, so a table that moved between two calls cannot pay the second place's money to a man who
  was not there the first time, and so a window closed twice (once by its last match, once by a
  process that was down over the weekend) pays once. The Supercup is not on the list: it
  is one match, and an artilharia of one evening would be a striker's single goal paid a
  season's first prize.

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

## The Club's Colours

A club's badge and its two shirts are drawn by its manager and read off his club everywhere, and
the rules below are the ones the code cannot be allowed to drift on.

- **The two colours have one job each.** On a crest the primary is everything in the foreground —
  the border and the lettering — and the secondary is the field the elements sit on. A crest is
  stroked in the primary on all ten shapes, because the border is what makes a badge recognisable
  at twenty pixels in a table.
- **A third colour belongs to an element and not to the club.** `CrestText` and `CrestEmblem` each
  carry their own colour, and a red and black club writing itself in white is the ordinary case: a
  colour picked from the club's own pair would make that club impossible to draw. A kit's third
  colour is the one the number and the collar are read in.
- **The position of an element is a fraction of the field, not a pixel**, and it is clamped to a
  range per element kind (`CrestText.LowestPosition`, `CrestEmblem.LowestPosition`). The badge is
  drawn at twenty pixels in a table and a hundred and sixty on the club's own page, so the same
  fraction has to mean the same place in both — and because the position is the *middle* of the
  element, an unclamped 0 would hang half of a figure out of the top of the shield.
- **A badge is the JSON of a choice and never a second copy of the design's shape.** `teams` holds
  `crest_json`, `home_kit_json` and `away_kit_json` as `jsonb`, and `CrestDesign.Parse` /
  `KitDesign.Parse` answer null rather than throwing: a club whose column was written by hand is a
  club whose shield is the initials placeholder again, which is what it was before crests existed.
- **A club is drawn, not asked.** `ClubIdentityDefaults` works a badge and two shirts out of the
  club's name with FNV-1a, so it is the same every time a world is seeded, and the seeder gives one
  to every club that has none. The default never overwrites a manager's drawing — it fills a gap.
- **The two shirts exist because one of them clashes, and which one is drawn from the match.**
  `KitClash.AreIndistinguishable` compares the two *bodies* by WCAG contrast and nothing else: two
  dark bodies are one dark mass however differently they are trimmed, and a trim is not what tells
  two teams apart. `KitClash.Decide` then draws over the pairs that can be told apart and not over
  all four combinations — a coin between two unreadable shirts decides nothing.
- **A match remembers the two shirts it was played in.** `Match.HomeKitSide` and
  `Match.AwayKitSide` are stamped at the kick-off that drew them, from the match's own seed, so a
  reload at minute seventy shows the same two shirts and an abandoned fixture replayed changes
  shirt the same way twice.
- **Only the club a manager is running may be drawn**, and the check is in `TeamService` rather
  than in the controller: the club screen, the club card and a background service all reach it
  through the same door, and a rule in the controller is a rule the other two walk past.
- **A crest with neither a letter nor a figure is a clearing, not a refusal.** It is a club whose
  shield goes back to the initials; the editor offers "sem escudo" rather than inventing a shape to
  send instead.

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

7. **A club carries its shield where its name is read** — `ClubCrest` in the sidebar, the
   scoreboard, the matchday, the next-match box and the live badge. A name identifies a club
   to someone reading it; a shield identifies it to someone glancing at a column of sixteen
   clubs, which is what a scoreboard and a matchday both are.

8. **A screen offers a decision only where the manager is entitled to one.** A match of two
   other clubs is a document everywhere in it: the eleven and the bench are read, the
   substitution panel and the interval button are not drawn, and the half-time dialog follows
   the server's `userTeamId` rather than the clock. The backend refuses a command for either
   club, so a control that appears anyway is a control the server has already said no to.

## A Number a Manager Budgets Against

Three screens used to print a rule the engine does not have, and they were the same rule: a
flat **"+20%"** beside a player's contract, on the squad table, on the player's page and in the
match's profile modal. A release is settled by `ReleaseRules` at half of what the club still
owes — the wages of the rounds left in the season plus the wages of every season the contract
promised after it — and no part of the game charges a fifth of anything. A manager reading
"+20%" beside a price is budgeting against a number the release will not take off his book, and
he finds out at the moment he presses the button.

- **A settlement is quoted by the rule that charges it.** `ReleaseRules.QuoteReleaseCost` takes
  what a squad row and a card actually hold — a season's wage, the rounds left in the season,
  the seasons left on the contract — and returns what `ReleaseRules.ReleaseCost` returns for
  the same contract. `SquadPlayerDto.ReleaseCost` and `PlayerProfileDto.ReleaseCost` are that
  number; the release command settles with the same call. Three answers to one question, and
  they cannot differ.
- **The rounds left are a fact about the calendar, not about a club.** Every club in a division
  has the same rounds left, so it is read once per squad from
  `ISeasonRepository.GetChampionshipRoundsLeftAsync` — which is why that question is asked of
  the season and not injected into every service as a helper. A round counts as played when its
  window is closed, never because its date has passed.
- **A percentage is not a currency and a currency is not a rule.** Any figure on a card that a
  manager would spend money on arrives from the DTO. A screen that cannot be told the number
  says so — it does not print a plausible one.

## An Absence Is Not a Zero

A missing value and a value of zero are different facts, and on this game's screens the
difference is the whole reading:

- **A club's balance is never drawn as L$ 0 before the books answer.** `FinanceiroScreen` used
  to render its summary and its pager outside the branch that guards the ledger, so a club with
  money in the bank was shown as penniless for as long as the request was in flight or kept
  failing. Both blocks are drawn only when `ledger` is there.
- **A statistics table is never drawn before the match has said it.** `MatchStats` fell back on
  seven rows of zeros with a 50/50 possession — and a goalless first half is *legitimately*
  seven rows of zeros, so the fallback could not be told from the result. It says the numbers
  are coming.
- **Added time and possession have no client-side default.** The engine draws the stoppage total
  at the whistle and announces each half; before it arrives the header says "—" rather than
  inventing three minutes, and the possession labels say "—" rather than claiming a split nobody
  has earned. The bar itself stays even, because a bar only has a width.
- **A `null` purse is not L$ 0.** `baseAmount` is null by design for an edition with no title's
  prize to share; the panels say the artilharia is not priced and leave the rows' em dashes
  alone. The percentages are still shown as the rule, against nothing.
- **A missing career, price or rating is a dash.** `TransferScreen` reads money and career
  totals as "—", and `StarRating` distinguishes a club with no stars from a club whose strength
  nobody has settled. Per-season card counts keep their zero, because a season in which he
  collected none is a real zero.
- **The narration belongs to the engine.** `MatchScreen`'s hero line draws
  `MatchNarration`'s own words or nothing; a sentence written in the client would be the only
  narration in the game no match ever said.

## One Rule, One Place

- **The attribute bands live in `attributeToneClass`** (`services/formatters.ts`, 1..100:
  `<35` red, `<65` amber, `>=65` green). `TacticsScreen` carried its own copy on the old 1..20
  bands (`<8`/`<14`), which painted every attribute above 14 green and made a squad of fourteen
  and a squad of ninety look the same.
- **The energy bands live in `energyClass` and `energyTextClass`,** side by side, for the bar and
  for the number. Four screens each had their own copy of the text version; they agreed by
  accident.
- **A domain constant is read, not restated.** `MatchRules.MaxSubstitutions`, the shootout's
  five kicks and the eleven plus seven of a squad are the engine's. Where a screen needs one as
  a bound and the DTO does not carry it, the honest answer is still not a second opinion in the
  client — so it is left as the one place it is written, and not copied.
- **A ground has the numbers the world keeps.** `StadiumScreen` reads `team.stadium` — a name, a
  capacity, one ticket price — and says what it does not have rather than filling it in. The
  city, the cup price at two and a half times the league one and the two sliders that wrote
  nowhere are gone; a control that moves a number nothing reads is a control that lied.

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
