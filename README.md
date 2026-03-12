# ElysStay — Backend

This README summarizes what's in the repository and shows step-by-step instructions for getting a developer environment up and running (clone, Docker, DB migrations, run the API). Commands are shown for Windows PowerShell.

## What's included
- `API/` — ASP.NET Core Web API (startup, controllers, auth wiring)
- `Application/` — Application layer (use cases, DTOs)
- `Domain/` — Domain entities, value objects, enums
- `Infrastructure/` — EF Core persistence, Identity, migrations
- `keycloak/` — Keycloak realm export used by the Docker Keycloak instance
- `docker-compose.yml` — Compose file to run Postgres + Keycloak
- `dev-tools/` — One-command dev launcher (also manages frontend sibling)
- `docs/` — Product spec, entity definitions, endpoints, business rules

## One-Command Dev Start (recommended)

> **Requires:** Docker Desktop (running), .NET 10 SDK, Node.js 18+, npm.
> **Expects** the frontend repo cloned as a sibling: `../ElysStay-fe/`

```powershell
.\dev-tools\dev.bat
```

This will:
1. Check prerequisites (Docker, .NET SDK, Node, npm)
2. Start infrastructure (Postgres on :5433, Keycloak on :8080)
3. Launch backend (.NET on :5027) in a named window
4. Launch frontend (Next.js on :3000) in a named window

### Other commands

```powershell
.\dev-tools\dev.bat -Status       # Show what's running
.\dev-tools\dev.bat -Kill         # Stop backend + frontend
.\dev-tools\dev.bat -Kill -Docker # Stop everything including Docker
.\dev-tools\dev.bat -Logs         # Tail service logs
.\dev-tools\dev.bat -Clean        # Nuclear reset (with confirmation)
.\dev-tools\dev.bat -Backend      # Start only backend
.\dev-tools\dev.bat -Frontend     # Start only frontend
.\dev-tools\dev.bat -Infra        # Start only Docker services
```

### Service ports

| Service | Port |
|---------|------|
| Backend API | [localhost:5027](http://localhost:5027) |
| Frontend | [localhost:3000](http://localhost:3000) |
| PostgreSQL | localhost:5433 |
| Keycloak Admin | [localhost:8080](http://localhost:8080) |
| Health Check | [localhost:5027/healthz](http://localhost:5027/healthz) |

## Manual Setup (alternative)

### Prerequisites
- Git
- .NET 10 SDK (dotnet) — confirm with `dotnet --version`
- Docker Desktop (with Compose)
- Optional: `dotnet-ef` global tool if you plan to run EF commands locally
  - Install: `dotnet tool install --global dotnet-ef`

## Quickstart (recommended for a developer)
1. Clone the repo

```powershell
git clone <repo-url> ElysStay
cd ElysStay
```

2. Build the solution to restore packages and verify compilation

```powershell
dotnet build ElysStay.slnx
```

3. Start Docker services (Postgres + Keycloak)

```powershell
# From the repo root
docker compose up -d
# Confirm services
docker compose ps
```

4. Verify the database is healthy

```powershell
# View logs if needed
docker logs elys_stay_db
# Check readiness via pg_isready (inside container)
docker exec -it elys_stay_db pg_isready -U postgres -d ElysStay
```

5. Run migrations (two options)

- Option A — Let the API auto-migrate on startup (current behavior in Dev):
  - The API's `Program.cs` will call `dbContext.Database.Migrate()` while running in the Development environment. Start the API locally (see next step) and it will apply migrations automatically.

- Option B — Run migrations explicitly (recommended for CI or manual control):

```powershell
# Ensure dotnet-ef is installed
dotnet tool install --global dotnet-ef
# Create / apply migrations (example: apply existing migrations)
dotnet ef database update --project Infrastructure --startup-project API
```

6. Run the API locally (Development env)

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
# Run API from repo root
dotnet run --project API
# Or run with logging/verbose output
dotnet run --project API --verbosity minimal
```

The API will be available at the port configured in the project (default: 5000/5001 for Kestrel with HTTPS). A health endpoint is available at `/healthz`.

## Notes about connection strings (docker vs local)
- `API/appsettings.json` currently contains a `DefaultConnection` pointing at `Host=localhost;Port=5432;...`.
- If you run Postgres in Docker and run the API locally, use `host.docker.internal` as the DB host or run the API inside Docker. Example connection string for local dev connecting to containerized DB:

```
Host=host.docker.internal;Port=5432;Database=ElysStay;Username=postgres;Password=supersecretpassword
```

- If running the API in a container with `docker compose`, set the DB host to `db` (service name) in the container environment so it can resolve the Postgres container.

## Authentication (Keycloak)
- Docker Compose starts Keycloak and imports the realm from `keycloak/elysstay-realm-export.json`.
- Admin console defaults (in compose): `admin:admin123` (only for dev). Do not use these credentials in production.
- The API is configured to validate JWT tokens from Keycloak based on the `Keycloak` settings in `API/appsettings.json`.

## Logging
- Serilog has been added and bootstrapped at startup. A minimal console sink is configured in `API/appsettings.json`.
- You can extend logging sinks (file, seq, etc.) via the Serilog configuration in `appsettings.*.json`.

## Useful commands and troubleshooting
- View running containers

```powershell
docker compose ps
```

- Tail logs

```powershell
docker compose logs -f api
docker compose logs -f elys_stay_db
docker compose logs -f elys_stay_keycloak
```

- If DB migrations fail with connection errors:
  - Ensure Postgres container is healthy: `docker ps` and `docker logs elys_stay_db`.
  - If running API locally, adjust the connection string to `host.docker.internal`.
  - If running API in container, ensure the connection string host is `db`.

- Common port conflicts: Keycloak uses 8080 and Postgres 5432 by default. Change ports in `docker-compose.yml` if needed.

## Security notes
- The provided docker-compose file and appsettings contain secrets for local development only. Move secrets to `.env` files, Docker secrets, or your secret store for staging/production.
- The CORS policy in `Program.cs` is permissive for dev; lock it down before deploying.

## Project conventions and useful locations
- Migrations: `Infrastructure/Migrations`
- EF DbContext: `Infrastructure/Persistence/ApplicationDbContext.cs`
- OpenAPI wiring: in `API/Program.cs`
- Keycloak realm export: `keycloak/elysstay-realm-export.json`

## Next recommended tasks for the team
- Add a `.env.example` and update `docker-compose.yml` to read from `.env` (avoid committing secrets).
- Add `make` / PowerShell scripts to start environment, apply migrations, and seed test data for new developers.
- Harden CORS and production authentication settings.
- Add CI pipeline step to run `dotnet build` and `dotnet ef database update` in a controlled manner.

## Contact / Onboarding tips
- When onboarding new devs, share the following checklist:
  1. Install prerequisites (Docker, .NET SDK)
  2. Clone repo
  3. Run `docker compose up -d`
  4. Run `dotnet run --project API` (or use the provided script)
  5. Open API root and `/healthz` to confirm

---

Requirements coverage for this task:
- Create a full README describing clone → docker → migrations → run: Done

If you want, I can next:
- Add `.env.example` and a `scripts/` PowerShell helper (start-dev.ps1) that runs docker compose, waits for DB health, runs migrations, and launches the API.
- Or add a minimal seed routine and a `seed` command.

Which of those would you like me to add next?
