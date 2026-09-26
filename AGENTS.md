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
