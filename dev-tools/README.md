# dev-tools — ElysStay Dev Launcher

One-command full-stack boot for local development.  
Manages Docker infra (Postgres + Keycloak), the .NET backend, and the Next.js frontend — all from a single entry point.

---

## Prerequisites

| Tool | Min version | Check |
|------|-------------|-------|
| Docker Desktop (running) | any recent | `docker --version` |
| .NET SDK | 10.x | `dotnet --version` |
| Node.js | 18+ | `node --version` |
| npm | 9+ | `npm --version` |

**Repo layout expected:**
```
<workspace>/
  ElysStay-be/    ← this repo (backend)
  ElysStay-fe/    ← frontend repo (must be a sibling folder)
```

---

## Quick Start

Open a terminal in `ElysStay-be/` and run:

```powershell
.\dev-tools\dev.bat
```

That's it. The launcher will:

1. **Pre-flight** — verify Docker, .NET SDK, Node, npm are all in PATH
2. **Infrastructure** — `docker compose up -d` (Postgres :5433, Keycloak :8080); waits up to 120s for both ports to respond
3. **Backend** — opens a dedicated PowerShell window running `dotnet run --launch-profile http`; auto-applies EF migrations on first start
4. **Frontend** — opens a dedicated PowerShell window running `npm run dev`; runs `npm install` first if `node_modules` is absent
5. **Env files** — auto-copies `.env.example → .env` (backend) and `.env.example → .env.local` (frontend) if neither exists

First boot is slower (~60-90s) due to Keycloak initialization and .NET cold compile. Subsequent boots are fast.

---

## Service URLs

| Service | URL | Notes |
|---------|-----|-------|
| Frontend | http://localhost:3000 | Next.js dev server |
| Backend API | http://localhost:5027 | ASP.NET Core (http profile) |
| Health check | http://localhost:5027/healthz | Returns `Healthy` when DB is reachable |
| OpenAPI schema | http://localhost:5027/openapi/v1.json | Raw JSON schema |
| Keycloak Admin | http://localhost:8080/admin | dev credentials: `admin` / `admin123` |
| PostgreSQL | localhost:5433 | user `postgres`, password in `.env` |

---

## All Commands

```powershell
# ── Default (start everything) ────────────────────────────
.\dev-tools\dev.bat               # start infra + backend + frontend

# ── Startup options ───────────────────────────────────────
.\dev-tools\dev.bat -Watch        # same as above + opens error-watcher window
.\dev-tools\dev.bat -Backend      # start only backend (infra must already be up)
.\dev-tools\dev.bat -Frontend     # start only frontend
.\dev-tools\dev.bat -Infra        # start only Docker (Postgres + Keycloak)

# ── Shutdown ──────────────────────────────────────────────
.\dev-tools\dev.bat -Kill         # stop backend + frontend processes
.\dev-tools\dev.bat -InfraDown    # stop Docker containers (data preserved)

# ── Diagnostics ───────────────────────────────────────────
.\dev-tools\dev.bat -Status       # show port status for all 4 services
.\dev-tools\dev.bat -Logs         # open logs folder in Explorer
.\dev-tools\dev.bat -ClearLogs    # wipe log files (no restart)

# ── Recovery ──────────────────────────────────────────────
.\dev-tools\dev.bat -FixKeycloak  # remove + recreate Keycloak container (re-imports realm)
.\dev-tools\dev.bat -Clean        # kill processes + wipe all build artifacts (bin/obj/.next)
```

> **After switching git branches:** always run `-Kill` then `-Clean` before booting again.  
> Stale build artifacts from a different branch will cause silent runtime failures.

---

## Log Files

Each boot creates timestamped log files in `dev-tools/logs/`:

```
logs/
  backend-2026-03-25_14-49-20.log   ← full dotnet run output
  frontend-2026-03-25_14-49-26.log  ← full npm run dev output
```

The dedicated service windows show only errors and startup confirmations.  
Full verbose output (every request, EF query, etc.) goes to the log file.

Use `-Watch` to open a third window that tails all log files and surfaces errors in real time.

---

## Troubleshooting

### Keycloak shows "unhealthy" in `docker ps`
The Docker healthcheck uses `curl` against the OIDC discovery endpoint. On first start Keycloak takes 60-90s to initialize — the container will flip to healthy once it finishes importing the realm. The service is usable as soon as `:8080` is reachable, regardless of Docker's health status.

If it stays unhealthy after 2 minutes: run `.\dev-tools\dev.bat -FixKeycloak`.

### Backend crashes on startup
1. Check `dev-tools/logs/backend-*.log` for the error.
2. Most common cause: Postgres not yet ready when the API starts. Run `.\dev-tools\dev.bat -Status` — if Postgres is DOWN, start infra first with `.\dev-tools\dev.bat -Infra`, then `.\dev-tools\dev.bat -Backend`.
3. Migration failure: check the log for EF error output. If the schema is broken, run `.\dev-tools\dev.bat -InfraDown` to wipe containers, then boot fresh.

### Port already in use
Run `.\dev-tools\dev.bat -Kill` first, then start again. If a port is still taken after `-Kill`, find the process manually:
```powershell
Get-NetTCPConnection -LocalPort 5027 -State Listen | Select-Object OwningProcess
```

### Frontend `npm install` keeps running
Delete `ElysStay-fe/node_modules/` and let the launcher reinstall from scratch.

### `.env` credentials
For local dev the `.env.example` defaults work as-is — no editing required.  
Do **not** commit a `.env` file with real credentials to the repo.

---

## Files in this folder

| File | Purpose |
|------|---------|
| `dev.bat` | Entry point — thin wrapper that calls `scripts/dev-mode.ps1` via PowerShell |
| `scripts/dev-mode.ps1` | Full launcher logic (pre-flight, infra, backend, frontend, watcher) |
| `scripts/clean.bat` | Nuclear reset: kills everything, stops Docker, deletes all build artifacts |
| `scripts/restart.bat` | Kill + restart all services |
| `scripts/stop_all.bat` | Stop all processes without cleaning |
| `logs/` | Timestamped service log files (gitignored) |
