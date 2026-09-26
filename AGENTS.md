# Football Manager — Development Guide

## Project Structure

```
frontend/football-manager-web/    React + TypeScript frontend
backend/                          .NET 10 backend (Api, Application, Domain, Infrastructure)
tests/                            xUnit test projects
```

## Frontend Commands

```bash
cd frontend/football-manager-web
npm install         # install dependencies
npm run dev         # start Vite dev server (http://localhost:5173)
npm run build       # build for production
npm run lint        # run ESLint
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
dotnet run --project FootballManager.Api  # run the API
dotnet test                           # run all tests
dotnet ef migrations add <Name>       # add a migration
dotnet ef database update             # apply migrations
dotnet ef migrations add <Name> --project FootballManager.Infrastructure --startup-project FootballManager.Infrastructure
```

The API runs on `http://localhost:5100` with Hot Reload enabled.

## Database

PostgreSQL 18 in Docker, listening on port `5433` (database `football_manager`):

```bash
docker start football-manager-postgres     # start if it is stopped
docker logs football-manager-postgres
```

Connection string lives in `backend/FootballManager.Api/appsettings.json` under
`ConnectionStrings:FootballManager`.

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
POST /competition

GET  /season                                POST /league/setup
GET  /season/{id}                           GET  /league/standing/{competitionSeasonId}
GET  /season/current                        GET  /league/standing/{competitionSeasonId}/team/{teamId}
POST /season                                GET  /league/scorer/{seasonId}?topN=
```

Domain errors return RFC 7807 with a stable `code` (e.g. `TeamNotFound`,
`SeasonRequired`, `NotEnoughTeams`); the frontend never parses messages to learn rules.
Malformed requests (an id that is not a guid, a missing field) answer 400 with
`code: ValidationFailed`. `POST /league/setup` is idempotent: when the edition already
has a schedule it returns the existing one instead of creating a second one.

The flow is: pick a club (`#/`), see the fixtures (`#/league`), choose the eleven
(`#/match/lineup/{fixtureId}`), then play (`#/match/{matchId}`). The lineup screen
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

The REST match commands and the hub expose the same behaviour, so the game works over
REST alone and SignalR only replaces polling.

Two rules the code depends on:

- Enums must serialize as **names** on both transports. MVC needs
  `AddControllers().AddJsonOptions(...)` and SignalR needs
  `AddSignalR().AddJsonProtocol(...)`; setting only one leaves the other emitting
  numbers while the frontend compares against names.
- The match row is rewritten on a cadence (every 5 minutes plus any goal, card,
  substitution or half-time), but **events are committed on every tick**. Each tick
  runs in its own DI scope, so anything left unsaved is discarded.


## Frontend Architecture

- **pages/**: Screen components (StartScreen, LeagueScreen, SquadScreen, MatchScreen, TeamViewScreen)
- **components/**: Reusable sub-components (League/, Match/, Modals/)
- **api/**: REST API client (axios-based)
- **signalr/**: SignalR client for real-time match updates
- **state/**: Zustand store for global frontend state
- **services/**: Custom hooks and utility formatters
- **types/**: TypeScript interfaces matching C# DTOs

## Key Rules

1. **Backend is the authority** — frontend never computes match results or standings
2. **DTOs are the contract** — TypeScript types must match C# response DTOs
3. **Single source of truth for match state** — backend MatchEngine
4. **Frontend only presents** — animations, transitions, and visual state are presentation-only
