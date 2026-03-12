# ============================================================================
# ElysStay Dev Mode - All-in-One Service Launcher
# ============================================================================
#
# Usage:
#   .\dev-mode.ps1              Start all (infra + backend + frontend)
#   .\dev-mode.ps1 -Watch       Start all + centralized error watcher
#   .\dev-mode.ps1 -Status      Show service status only
#   .\dev-mode.ps1 -Logs        Open logs folder
#   .\dev-mode.ps1 -ClearLogs   Clear logs only (no restart)
#   .\dev-mode.ps1 -Kill        Kill all dev processes (dotnet + node)
#   .\dev-mode.ps1 -Clean       Clean all build artifacts (bin/obj/.next)
#   .\dev-mode.ps1 -Infra       Start Docker infrastructure only
#   .\dev-mode.ps1 -InfraDown   Stop Docker infrastructure
#   .\dev-mode.ps1 -FixKeycloak Purge Keycloak and restart fresh
#   .\dev-mode.ps1 -Backend     Start backend only (infra must be up)
#   .\dev-mode.ps1 -Frontend    Start frontend only
#
# IMPORTANT: After switching git branches, run -Kill then -Clean first!
# IMPORTANT: This file MUST stay ASCII-only for PS 5.1 compatibility!
#
# ============================================================================

param(
    [switch]$Watch,
    [switch]$Status,
    [switch]$Logs,
    [switch]$ClearLogs,
    [switch]$Kill,
    [switch]$Clean,
    [switch]$Infra,
    [switch]$InfraDown,
    [switch]$FixKeycloak,
    [switch]$Backend,
    [switch]$Frontend
)

# ============================================================================
# Configuration
# ============================================================================

$ErrorActionPreference = "Stop"

# Calculate paths from this script's location
# Layout: ElysStay-be/dev-tools/scripts/dev-mode.ps1
#         ElysStay-be/ = backend repo root ($REPO_DIR)
#         ../ElysStay-fe/ = frontend repo (sibling of backend)
$SCRIPT_DIR = Split-Path -Parent $MyInvocation.MyCommand.Path
$TOOLS_DIR = Split-Path -Parent $SCRIPT_DIR
$REPO_DIR = Split-Path -Parent $TOOLS_DIR
$WORKSPACE_DIR = Split-Path -Parent $REPO_DIR

$LOG_DIR = Join-Path $TOOLS_DIR "logs"

$PROJECT_NAME = "ElysStay"

# Backend (.NET 10) - this repo
$BACKEND_DIR = $REPO_DIR
$BACKEND_API_DIR = Join-Path $BACKEND_DIR "API"
$BACKEND_PORT = 5027

# Frontend (Next.js) - sibling repo
$FRONTEND_DIR = Join-Path $WORKSPACE_DIR "ElysStay-fe"
$FRONTEND_PORT = 3000

# Docker compose
$COMPOSE_FILE = Join-Path $REPO_DIR "docker-compose.yml"

# Infrastructure ports (from docker-compose.yml)
$INFRA_PORTS = [ordered]@{
    Postgres = 5433
    Keycloak = 8080
}

# Service colors for error watcher
$SERVICE_COLORS = @{
    'backend'  = 'Cyan'
    'frontend' = 'Green'
}

# ============================================================================
# Utility Functions
# ============================================================================

function Test-Port {
    param([int]$Port)
    try {
        $client = New-Object Net.Sockets.TcpClient
        $client.Connect("localhost", $Port)
        $client.Close()
        return $true
    } catch {
        return $false
    }
}

function Write-Status {
    param([string]$Message, [string]$Type = "INFO")
    $ts = Get-Date -Format "HH:mm:ss"
    switch ($Type) {
        "OK"    { Write-Host "[$ts] " -NoNewline -ForegroundColor DarkGray; Write-Host "[OK] " -NoNewline -ForegroundColor Green; Write-Host $Message }
        "WAIT"  { Write-Host "[$ts] " -NoNewline -ForegroundColor DarkGray; Write-Host "[..] " -NoNewline -ForegroundColor Yellow; Write-Host $Message }
        "FAIL"  { Write-Host "[$ts] " -NoNewline -ForegroundColor DarkGray; Write-Host "[XX] " -NoNewline -ForegroundColor Red; Write-Host $Message }
        "INFO"  { Write-Host "[$ts] " -NoNewline -ForegroundColor DarkGray; Write-Host "[--] " -NoNewline -ForegroundColor Cyan; Write-Host $Message }
        default { Write-Host "[$ts] $Message" }
    }
}

function Show-Banner {
    Write-Host ""
    Write-Host "  ================================================================" -ForegroundColor Cyan
    Write-Host "       $PROJECT_NAME Dev Mode                                     " -ForegroundColor Cyan
    Write-Host "  ================================================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "  Workspace:  $WORKSPACE_DIR" -ForegroundColor DarkGray
    Write-Host "  Backend:    $BACKEND_DIR" -ForegroundColor DarkGray
    Write-Host "  Frontend:   $FRONTEND_DIR" -ForegroundColor DarkGray
    Write-Host "  Logs:       $LOG_DIR" -ForegroundColor DarkGray
    Write-Host ""
}

# ============================================================================
# Pre-flight Checks
# ============================================================================

function Test-Preflight {
    $passed = $true

    # Docker
    try {
        $null = & docker --version 2>&1
        if ($LASTEXITCODE -ne 0) { throw "bad exit" }
        Write-Status "Docker found" "OK"
    } catch {
        Write-Status "Docker not found or not running" "FAIL"
        $passed = $false
    }

    # .NET SDK
    try {
        $dotnetVer = & dotnet --version 2>&1
        if ($LASTEXITCODE -ne 0) { throw "bad exit" }
        Write-Status ".NET SDK: $dotnetVer" "OK"
    } catch {
        Write-Status ".NET SDK not found in PATH" "FAIL"
        $passed = $false
    }

    # Node.js
    try {
        $nodeVer = & node --version 2>&1
        if ($LASTEXITCODE -ne 0) { throw "bad exit" }
        Write-Status "Node.js: $nodeVer" "OK"
    } catch {
        Write-Status "Node.js not found in PATH" "FAIL"
        $passed = $false
    }

    # npm
    try {
        $npmVer = & npm --version 2>&1
        if ($LASTEXITCODE -ne 0) { throw "bad exit" }
        Write-Status "npm: $npmVer" "OK"
    } catch {
        Write-Status "npm not found in PATH" "FAIL"
        $passed = $false
    }

    # Docker compose file
    if (-not (Test-Path $COMPOSE_FILE)) {
        Write-Status "docker-compose.yml not found at $COMPOSE_FILE" "FAIL"
        $passed = $false
    }

    # Backend project
    $apiCsproj = Join-Path $BACKEND_API_DIR "API.csproj"
    if (-not (Test-Path $apiCsproj)) {
        Write-Status "API.csproj not found at $apiCsproj" "FAIL"
        $passed = $false
    }

    # Frontend package.json
    $pkgJson = Join-Path $FRONTEND_DIR "package.json"
    if (-not (Test-Path $pkgJson)) {
        Write-Status "package.json not found at $pkgJson" "FAIL"
        $passed = $false
    }

    # Frontend node_modules
    $nodeModules = Join-Path $FRONTEND_DIR "node_modules"
    if (-not (Test-Path $nodeModules)) {
        Write-Status "node_modules missing -- will install" "WAIT"
    }

    # Backend .env for Docker
    $envFile = Join-Path $BACKEND_DIR ".env"
    if (-not (Test-Path $envFile)) {
        Write-Status "Backend .env missing -- creating from .env.example" "WAIT"
        $envExample = Join-Path $BACKEND_DIR ".env.example"
        if (Test-Path $envExample) {
            Copy-Item $envExample $envFile
            Write-Status "Created .env from .env.example" "OK"
        } else {
            Write-Status "No .env.example found -- Docker may fail" "FAIL"
            $passed = $false
        }
    }

    # Frontend .env
    $feEnv = Join-Path $FRONTEND_DIR ".env.local"
    if (-not (Test-Path $feEnv)) {
        $feEnvBase = Join-Path $FRONTEND_DIR ".env"
        if (-not (Test-Path $feEnvBase)) {
            Write-Status "Frontend .env missing -- creating from .env.example" "WAIT"
            $feEnvExample = Join-Path $FRONTEND_DIR ".env.example"
            if (Test-Path $feEnvExample) {
                Copy-Item $feEnvExample (Join-Path $FRONTEND_DIR ".env.local")
                Write-Status "Created .env.local from .env.example" "OK"
            } else {
                Write-Status "No frontend .env.example found -- may use defaults" "INFO"
            }
        }
    }

    return $passed
}

# ============================================================================
# Command Handlers
# ============================================================================

function Show-ServiceStatus {
    Show-Banner
    Write-Host "  SERVICE STATUS" -ForegroundColor White
    Write-Host "  ----------------------------------------------------------------" -ForegroundColor DarkGray

    # Infrastructure
    Write-Host ""
    Write-Host "  Infrastructure:" -ForegroundColor DarkGray
    foreach ($name in $INFRA_PORTS.Keys) {
        $port = $INFRA_PORTS[$name]
        $up = Test-Port $port
        $status = if ($up) { "[UP]  " } else { "[DOWN]" }
        $color = if ($up) { "Green" } else { "Red" }
        Write-Host "    $status " -NoNewline -ForegroundColor $color
        Write-Host "$($name.PadRight(12)) :$port" -ForegroundColor White
    }

    # Application services
    Write-Host ""
    Write-Host "  Application:" -ForegroundColor DarkGray
    foreach ($entry in @(@{Name="Backend"; Port=$BACKEND_PORT}, @{Name="Frontend"; Port=$FRONTEND_PORT})) {
        $up = Test-Port $entry.Port
        $status = if ($up) { "[UP]  " } else { "[DOWN]" }
        $color = if ($up) { "Green" } else { "Red" }
        Write-Host "    $status " -NoNewline -ForegroundColor $color
        Write-Host "$($entry.Name.PadRight(12)) :$($entry.Port)" -ForegroundColor White
    }

    # Docker containers
    Write-Host ""
    Write-Host "  Docker Containers:" -ForegroundColor DarkGray
    try {
        $containers = docker ps --filter "name=elys_stay" --format "{{.Names}}: {{.Status}}" 2>&1
        if ($containers) {
            foreach ($c in $containers) {
                Write-Host "    $c" -ForegroundColor Gray
            }
        } else {
            Write-Host "    (none running)" -ForegroundColor DarkGray
        }
    } catch {
        Write-Host "    (docker not available)" -ForegroundColor DarkGray
    }

    Write-Host ""
}

function Open-LogsFolder {
    if (-not (Test-Path $LOG_DIR)) {
        New-Item -ItemType Directory -Path $LOG_DIR -Force | Out-Null
    }
    Start-Process explorer $LOG_DIR
    Write-Status "Opened logs folder: $LOG_DIR" "OK"
}

function Clear-Logs {
    if (Test-Path $LOG_DIR) {
        Remove-Item "$LOG_DIR\*" -Force -ErrorAction SilentlyContinue
        Write-Status "Cleared logs folder" "OK"
    }
}

function Stop-AllDevProcesses {
    Write-Status "Stopping dev processes..." "WAIT"

    # Kill by known ports (safer than killing all dotnet/node)
    # Include 5000 as fallback (.NET default if launch profile is not used)
    foreach ($port in @($BACKEND_PORT, $FRONTEND_PORT, 5000)) {
        try {
            $connections = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue
            foreach ($conn in $connections) {
                $proc = Get-Process -Id $conn.OwningProcess -ErrorAction SilentlyContinue
                if ($proc) {
                    Write-Status "Stopping $($proc.ProcessName) (PID $($proc.Id)) on :$port" "INFO"
                    Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
                }
            }
        } catch {
            # Fallback: use netstat
            $netstatOut = netstat -ano 2>$null | Select-String ":$port\s" | Select-String "LISTENING"
            foreach ($line in $netstatOut) {
                if ($line -match '\s(\d+)\s*$') {
                    $pid = $matches[1]
                    try {
                        Stop-Process -Id $pid -Force -ErrorAction SilentlyContinue
                        Write-Status "Stopped PID $pid on :$port" "INFO"
                    } catch {}
                }
            }
        }
    }

    Start-Sleep -Seconds 2
    Write-Status "Dev processes stopped" "OK"
}

function Clean-AllArtifacts {
    Write-Status "Cleaning all build artifacts..." "WAIT"

    # Backend: dotnet clean + remove bin/obj
    Write-Status "Cleaning .NET solution..." "INFO"
    $slnx = Join-Path $BACKEND_DIR "ElysStay.slnx"
    if (Test-Path $slnx) {
        Push-Location $BACKEND_DIR
        try {
            & dotnet clean ElysStay.slnx -q 2>&1 | Out-Null
            Write-Status "dotnet clean completed" "OK"
        } catch {
            Write-Status "dotnet clean failed -- removing bin/obj manually" "INFO"
        }
        Pop-Location
    }

    # Remove bin/obj directories
    foreach ($proj in @("API", "Application", "Domain", "Infrastructure")) {
        foreach ($dir in @("bin", "obj")) {
            $target = Join-Path $BACKEND_DIR (Join-Path $proj $dir)
            if (Test-Path $target) {
                Remove-Item $target -Recurse -Force -ErrorAction SilentlyContinue
                Write-Status "  Deleted $proj\$dir" "INFO"
            }
        }
    }

    # Frontend: remove .next
    $nextDir = Join-Path $FRONTEND_DIR ".next"
    if (Test-Path $nextDir) {
        Remove-Item $nextDir -Recurse -Force -ErrorAction SilentlyContinue
        Write-Status "  Deleted .next/" "INFO"
    }

    # Clear logs too
    Clear-Logs

    Write-Status "All artifacts cleaned" "OK"
}

function Start-InfrastructureOnly {
    # Auto-create .env if missing
    $envFile = Join-Path $BACKEND_DIR ".env"
    if (-not (Test-Path $envFile)) {
        $envExample = Join-Path $BACKEND_DIR ".env.example"
        if (Test-Path $envExample) {
            Copy-Item $envExample $envFile
            Write-Status "Created .env from .env.example" "OK"
        }
    }

    Write-Status "Starting Docker infrastructure..." "WAIT"
    Push-Location $BACKEND_DIR
    $prevErrorAction = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    $composeOutput = & docker compose up -d 2>&1
    $composeExitCode = $LASTEXITCODE
    $ErrorActionPreference = $prevErrorAction
    if ($composeExitCode -ne 0) {
        Write-Status "docker compose up failed (exit $composeExitCode)" "FAIL"
        foreach ($line in $composeOutput) {
            Write-Host "    $line" -ForegroundColor DarkGray
        }
        Pop-Location
        return
    }
    Pop-Location

    Write-Status "Waiting for infrastructure..." "WAIT"

    # Wait with progress -- Keycloak can take 60-90s on first start
    $deadline = (Get-Date).AddSeconds(120)
    while ((Get-Date) -lt $deadline) {
        $allUp = $true
        foreach ($name in $INFRA_PORTS.Keys) {
            if (-not (Test-Port $INFRA_PORTS[$name])) { $allUp = $false; break }
        }
        if ($allUp) { break }
        Start-Sleep -Seconds 3
    }

    # Report status
    foreach ($name in $INFRA_PORTS.Keys) {
        $port = $INFRA_PORTS[$name]
        $status = if (Test-Port $port) { "OK" } else { "FAIL" }
        Write-Status "$($name.PadRight(12)) :$port" $status
    }
}

function Stop-Infrastructure {
    Write-Status "Stopping Docker infrastructure..." "WAIT"
    Push-Location $BACKEND_DIR
    $prevEA = $ErrorActionPreference; $ErrorActionPreference = "Continue"
    $null = & docker compose down 2>&1
    $ErrorActionPreference = $prevEA
    Pop-Location
    Write-Status "Infrastructure stopped" "OK"
}

function Repair-Keycloak {
    # Keycloak in this project uses Postgres (not H2), but the container
    # itself can get into bad state. Recovery: remove container + recreate.
    # Realm re-imports from the mounted /opt/keycloak/data/import volume.

    Show-Banner
    Write-Status "Stopping Keycloak container..." "WAIT"

    Push-Location $BACKEND_DIR
    $prevEA = $ErrorActionPreference; $ErrorActionPreference = "Continue"

    # Force-stop and remove the container
    $null = & docker compose rm -f -s keycloak 2>&1

    # Recreate fresh
    Write-Status "Recreating Keycloak container..." "WAIT"
    $null = & docker compose up -d keycloak 2>&1
    $ErrorActionPreference = $prevEA
    Pop-Location

    # Wait for Keycloak to initialize (imports realm on fresh start)
    $deadline = (Get-Date).AddSeconds(120)
    while ((Get-Date) -lt $deadline) {
        if (Test-Port 8080) {
            Write-Status "Keycloak is UP on :8080" "OK"
            return
        }
        Start-Sleep -Seconds 3
    }
    Write-Status "Keycloak did not respond within 120s -- check: docker logs elys_stay_keycloak" "FAIL"
}

# ============================================================================
# Service Launcher -- The Cunning Part
# ============================================================================

function Start-BackendInWindow {
    if (Test-Port $BACKEND_PORT) {
        Write-Status "Backend already running on :$BACKEND_PORT" "OK"
        return
    }

    if (-not (Test-Path $LOG_DIR)) {
        New-Item -ItemType Directory -Path $LOG_DIR -Force | Out-Null
    }

    $timestamp = Get-Date -Format "yyyy-MM-dd_HH-mm-ss"
    $logFile = Join-Path $LOG_DIR "backend-$timestamp.log"

    # The inner script runs inside a NEW PowerShell window
    # CUNNING: Console shows ONLY errors + startup confirmation.
    # Full output goes to log file for forensics.
    $innerScript = @"
`$ErrorActionPreference = 'Continue'
`$host.UI.RawUI.WindowTitle = '[$PROJECT_NAME] backend ... (:$BACKEND_PORT)'

`$logFile = '$logFile'
`$serviceDir = '$BACKEND_API_DIR'
`$errorCount = 0

Write-Host '' -ForegroundColor Cyan
Write-Host '  ============================================' -ForegroundColor Cyan
Write-Host '    ELYSSTAY BACKEND  --  port $BACKEND_PORT' -ForegroundColor Cyan
Write-Host '  ============================================' -ForegroundColor Cyan
Write-Host "  Log: `$logFile" -ForegroundColor DarkGray
Write-Host ''

function Process-Line {
    param([string]`$Line)

    # ALWAYS write to log file
    Add-Content -Path `$logFile -Value `$Line -ErrorAction SilentlyContinue

    # Skip noise -- handler/resolver class name mentions
    if (`$Line -match 'ExceptionHandler|ExceptionResolver|GlobalExceptionHandler') {
        return
    }

    # ERRORS -- show to console
    if (`$Line -match '\[(ERR|FTL|err|ftl)\]|Unhandled exception|FATAL|System\..*Exception:') {
        `$script:errorCount++
        Write-Host `$Line -ForegroundColor Red
        `$host.UI.RawUI.WindowTitle = '[$PROJECT_NAME] backend ERR! (:$BACKEND_PORT)'
    }
    elseif (`$Line -match '^\s+at\s|^\s+---') {
        # Stack trace lines
        Write-Host `$Line -ForegroundColor DarkRed
    }
    elseif (`$Line -match 'Now listening on|Application started|Content root path') {
        # SUCCESS -- .NET startup confirmation
        Write-Host `$Line -ForegroundColor Green
        `$host.UI.RawUI.WindowTitle = '[$PROJECT_NAME] backend OK (:$BACKEND_PORT)'
    }
    elseif (`$Line -match 'Database migration|Applying migration|Applied migration') {
        # Migration progress -- useful to see
        Write-Host `$Line -ForegroundColor Yellow
    }
    elseif (`$Line -match '\[WRN\]|warn:') {
        # Warnings -- dim but visible
        Write-Host `$Line -ForegroundColor DarkYellow
    }
    # Everything else: SILENT (goes to file only)
}

Set-Location `$serviceDir

# Run the .NET backend -- pipe through smart filter
& dotnet run --launch-profile http 2>&1 | ForEach-Object { Process-Line `$_ }

`$exitCode = `$LASTEXITCODE
Write-Host ''
if (`$exitCode -ne 0) {
    Write-Host "=== CRASHED (exit `$exitCode) ===" -ForegroundColor Red
    Write-Host 'Last 30 lines:' -ForegroundColor DarkGray
    if (Test-Path `$logFile) {
        Get-Content `$logFile -Tail 30 | ForEach-Object { Write-Host `$_ -ForegroundColor DarkGray }
    }
} else {
    Write-Host '=== STOPPED ===' -ForegroundColor Yellow
}
Write-Host ''
Read-Host 'Press Enter to close'
"@

    $bytes = [System.Text.Encoding]::Unicode.GetBytes($innerScript)
    $encoded = [Convert]::ToBase64String($bytes)

    Start-Process powershell -ArgumentList "-ExecutionPolicy", "Bypass", "-EncodedCommand", $encoded
    Write-Status "Backend starting on :$BACKEND_PORT (log: backend-$timestamp.log)" "WAIT"
}

function Start-FrontendInWindow {
    if (Test-Port $FRONTEND_PORT) {
        Write-Status "Frontend already running on :$FRONTEND_PORT" "OK"
        return
    }

    if (-not (Test-Path $LOG_DIR)) {
        New-Item -ItemType Directory -Path $LOG_DIR -Force | Out-Null
    }

    # Install node_modules if missing
    $nodeModules = Join-Path $FRONTEND_DIR "node_modules"
    if (-not (Test-Path $nodeModules)) {
        Write-Status "Installing frontend dependencies..." "WAIT"
        Push-Location $FRONTEND_DIR
        & npm install 2>&1 | Out-Null
        Pop-Location
        Write-Status "npm install completed" "OK"
    }

    # Create .env.local from .env.example if no .env file exists
    $feEnvLocal = Join-Path $FRONTEND_DIR ".env.local"
    $feEnvBase = Join-Path $FRONTEND_DIR ".env"
    if ((-not (Test-Path $feEnvLocal)) -and (-not (Test-Path $feEnvBase))) {
        $feExample = Join-Path $FRONTEND_DIR ".env.example"
        if (Test-Path $feExample) {
            Copy-Item $feExample $feEnvLocal
            Write-Status "Created .env.local from .env.example" "OK"
        }
    }

    $timestamp = Get-Date -Format "yyyy-MM-dd_HH-mm-ss"
    $logFile = Join-Path $LOG_DIR "frontend-$timestamp.log"

    $innerScript = @"
`$ErrorActionPreference = 'Continue'
`$host.UI.RawUI.WindowTitle = '[$PROJECT_NAME] frontend ... (:$FRONTEND_PORT)'

`$logFile = '$logFile'
`$serviceDir = '$FRONTEND_DIR'
`$errorCount = 0

Write-Host '' -ForegroundColor Green
Write-Host '  ============================================' -ForegroundColor Green
Write-Host '    ELYSSTAY FRONTEND  --  port $FRONTEND_PORT' -ForegroundColor Green
Write-Host '  ============================================' -ForegroundColor Green
Write-Host "  Log: `$logFile" -ForegroundColor DarkGray
Write-Host ''

function Process-Line {
    param([string]`$Line)

    # ALWAYS write to log file
    Add-Content -Path `$logFile -Value `$Line -ErrorAction SilentlyContinue

    # ERRORS
    if (`$Line -match 'Error:|error:|ERROR|Failed to compile|Module not found|SyntaxError|TypeError') {
        `$script:errorCount++
        Write-Host `$Line -ForegroundColor Red
        `$host.UI.RawUI.WindowTitle = '[$PROJECT_NAME] frontend ERR! (:$FRONTEND_PORT)'
    }
    elseif (`$Line -match 'warning|WARN') {
        Write-Host `$Line -ForegroundColor DarkYellow
    }
    elseif (`$Line -match 'Ready in|ready on|Local:|started server on|Compiled') {
        # SUCCESS -- Next.js startup confirmation
        Write-Host `$Line -ForegroundColor Green
        `$host.UI.RawUI.WindowTitle = '[$PROJECT_NAME] frontend OK (:$FRONTEND_PORT)'
    }
    elseif (`$Line -match 'Compiling|Building|Bundling') {
        Write-Host `$Line -ForegroundColor DarkCyan
    }
    # Everything else: SILENT (goes to file only)
}

Set-Location `$serviceDir

& npm run dev 2>&1 | ForEach-Object { Process-Line `$_ }

`$exitCode = `$LASTEXITCODE
Write-Host ''
if (`$exitCode -ne 0) {
    Write-Host "=== CRASHED (exit `$exitCode) ===" -ForegroundColor Red
    Write-Host 'Last 30 lines:' -ForegroundColor DarkGray
    if (Test-Path `$logFile) {
        Get-Content `$logFile -Tail 30 | ForEach-Object { Write-Host `$_ -ForegroundColor DarkGray }
    }
} else {
    Write-Host '=== STOPPED ===' -ForegroundColor Yellow
}
Write-Host ''
Read-Host 'Press Enter to close'
"@

    $bytes = [System.Text.Encoding]::Unicode.GetBytes($innerScript)
    $encoded = [Convert]::ToBase64String($bytes)

    Start-Process powershell -ArgumentList "-ExecutionPolicy", "Bypass", "-EncodedCommand", $encoded
    Write-Status "Frontend starting on :$FRONTEND_PORT (log: frontend-$timestamp.log)" "WAIT"
}

# ============================================================================
# Error Watcher -- Centralized Error View
# ============================================================================

function Start-ErrorWatcher {
    if (-not (Test-Path $LOG_DIR)) {
        New-Item -ItemType Directory -Path $LOG_DIR -Force | Out-Null
    }

    $colorsString = ""
    foreach ($key in $SERVICE_COLORS.Keys) {
        $colorsString += "'$key' = '$($SERVICE_COLORS[$key])'`n    "
    }

    $watcherScript = @"
`$host.UI.RawUI.WindowTitle = '$PROJECT_NAME Error Watcher'
`$LogDir = '$LOG_DIR'

Write-Host ''
Write-Host '  ============================================' -ForegroundColor Red
Write-Host '    ELYSSTAY ERROR WATCHER' -ForegroundColor Red
Write-Host '  ============================================' -ForegroundColor Red
Write-Host "  Logs: `$LogDir" -ForegroundColor DarkGray
Write-Host '  Shows: Errors, exceptions, stack traces, build failures' -ForegroundColor DarkGray
Write-Host ''

`$filePositions = @{}

`$serviceColors = @{
    $colorsString
}

while (`$true) {
    `$files = Get-ChildItem -Path `$LogDir -Filter '*.log' -ErrorAction SilentlyContinue

    foreach (`$file in `$files) {
        `$path = `$file.FullName

        # Extract service name from filename (e.g. "backend" from "backend-2025-01-01_12-30-00.log")
        `$svcName = 'unknown'
        if (`$file.Name -match '^([a-z]+)-\d{4}') {
            `$svcName = `$matches[1]
        }

        `$color = if (`$serviceColors.ContainsKey(`$svcName)) { `$serviceColors[`$svcName] } else { 'Gray' }

        if (-not `$filePositions.ContainsKey(`$path)) {
            `$filePositions[`$path] = 0
        }

        try {
            `$content = Get-Content `$path -ErrorAction SilentlyContinue
            `$lineCount = `$content.Count

            if (`$lineCount -gt `$filePositions[`$path]) {
                `$newLines = `$content[`$filePositions[`$path]..(`$lineCount - 1)]
                `$filePositions[`$path] = `$lineCount

                foreach (`$line in `$newLines) {
                    # .NET errors
                    `$isDotnetError = `$line -match '\[(ERR|FTL|err|ftl)\]|Unhandled exception|FATAL'
                    # .NET exception types
                    `$isException = `$line -match 'System\..*Exception:|Microsoft\..*Exception:|Npgsql\..*Exception:'
                    # Stack traces
                    `$isStackTrace = `$line -match '^\s+at\s|^\s+---'
                    # Next.js errors
                    `$isNodeError = `$line -match 'Error:|error:|Failed to compile|Module not found|SyntaxError|TypeError'
                    # Build failures
                    `$isBuildError = `$line -match 'Build FAILED|error MSB|error CS|COMPILATION ERROR'

                    # Noise filter
                    `$isNoise = `$line -match 'ExceptionHandler|ExceptionResolver|GlobalExceptionHandler'
                    `$isNoise = `$isNoise -or (`$line -match '\[INF\]|\[DBG\]|\[WRN\].*: \[Consumer')

                    `$shouldShow = (`$isDotnetError -or `$isException -or `$isStackTrace -or `$isNodeError -or `$isBuildError) -and (-not `$isNoise)

                    if (`$shouldShow) {
                        `$ts = Get-Date -Format 'HH:mm:ss'

                        if (`$isStackTrace) {
                            Write-Host "         `$line" -ForegroundColor DarkRed
                        }
                        elseif (`$isException) {
                            Write-Host ""
                            Write-Host "[`$ts] " -NoNewline -ForegroundColor DarkGray
                            Write-Host "`$(`$svcName.ToUpper().PadRight(12))" -NoNewline -ForegroundColor `$color
                            Write-Host "`$line" -ForegroundColor Red
                        }
                        else {
                            Write-Host ""
                            Write-Host "[`$ts] " -NoNewline -ForegroundColor DarkGray
                            Write-Host "`$(`$svcName.ToUpper().PadRight(12))" -NoNewline -ForegroundColor `$color
                            Write-Host `$line -ForegroundColor Red
                        }
                    }
                }
            }
        } catch { }
    }

    Start-Sleep -Milliseconds 300
}
"@

    $bytes = [System.Text.Encoding]::Unicode.GetBytes($watcherScript)
    $encoded = [Convert]::ToBase64String($bytes)

    Start-Process powershell -ArgumentList "-ExecutionPolicy", "Bypass", "-EncodedCommand", $encoded
    Write-Status "Error watcher started" "OK"
}

# ============================================================================
# Main Flow
# ============================================================================

# Handle simple commands first
if ($Status) {
    Show-ServiceStatus
    exit 0
}

if ($Logs) {
    Open-LogsFolder
    exit 0
}

if ($Kill) {
    Show-Banner
    Stop-AllDevProcesses
    exit 0
}

if ($ClearLogs) {
    Clear-Logs
    exit 0
}

if ($Clean) {
    Show-Banner
    Stop-AllDevProcesses
    Clean-AllArtifacts
    exit 0
}

if ($Infra) {
    Show-Banner
    Start-InfrastructureOnly
    exit 0
}

if ($InfraDown) {
    Show-Banner
    Stop-Infrastructure
    exit 0
}

if ($FixKeycloak) {
    Show-Banner
    Repair-Keycloak
    exit 0
}

if ($Backend) {
    Show-Banner
    Start-BackendInWindow
    exit 0
}

if ($Frontend) {
    Show-Banner
    Start-FrontendInWindow
    exit 0
}

# ============================================================================
# Full Startup (default mode)
# ============================================================================

# Fresh run = fresh logs
Clear-Logs

Show-Banner

# Pre-flight checks
Write-Host "  [0/4] Pre-flight Checks" -ForegroundColor White
Write-Host "  ----------------------------------------------------------------" -ForegroundColor DarkGray
if (-not (Test-Preflight)) {
    Write-Status "Pre-flight checks failed. Fix issues above and retry." "FAIL"
    exit 1
}
Write-Host ""

# Step 1: Infrastructure
Write-Host "  [1/4] Infrastructure (Docker)" -ForegroundColor White
Write-Host "  ----------------------------------------------------------------" -ForegroundColor DarkGray

$anyInfraDown = $false
foreach ($name in $INFRA_PORTS.Keys) {
    if (-not (Test-Port $INFRA_PORTS[$name])) { $anyInfraDown = $true; break }
}

if ($anyInfraDown) {
    Start-InfrastructureOnly
} else {
    foreach ($name in $INFRA_PORTS.Keys) {
        $port = $INFRA_PORTS[$name]
        Write-Status "$($name.PadRight(12)) :$port (already up)" "OK"
    }
}
Write-Host ""

# Step 2: Backend (.NET)
Write-Host "  [2/4] Backend (.NET)" -ForegroundColor White
Write-Host "  ----------------------------------------------------------------" -ForegroundColor DarkGray
Start-BackendInWindow

# Give backend a moment to begin starting before launching frontend
# This avoids a thundering herd on CPU
Start-Sleep -Seconds 3
Write-Host ""

# Step 3: Frontend (Next.js)
Write-Host "  [3/4] Frontend (Next.js)" -ForegroundColor White
Write-Host "  ----------------------------------------------------------------" -ForegroundColor DarkGray
Start-FrontendInWindow
Write-Host ""

# Step 4: Error watcher (optional)
Write-Host "  [4/4] Error Watcher" -ForegroundColor White
Write-Host "  ----------------------------------------------------------------" -ForegroundColor DarkGray
if ($Watch) {
    Start-ErrorWatcher
} else {
    Write-Status "Skipped (use -Watch flag to enable)" "INFO"
}

# Final summary
Write-Host ""
Write-Host "  ================================================================" -ForegroundColor Green
Write-Host "       $PROJECT_NAME Dev Mode Active!                             " -ForegroundColor Green
Write-Host "  ================================================================" -ForegroundColor Green
Write-Host ""
Write-Host "  Services (starting up -- give them ~15-30s):" -ForegroundColor White
Write-Host "    Postgres       http://localhost:$($INFRA_PORTS.Postgres) (Docker)" -ForegroundColor Gray
Write-Host "    Keycloak       http://localhost:$($INFRA_PORTS.Keycloak) (Docker)" -ForegroundColor Gray
Write-Host "    Backend        http://localhost:$BACKEND_PORT" -ForegroundColor Gray
Write-Host "    Frontend       http://localhost:$FRONTEND_PORT" -ForegroundColor Gray
Write-Host ""
Write-Host "  Useful URLs:" -ForegroundColor White
Write-Host "    Health check:  http://localhost:$BACKEND_PORT/healthz" -ForegroundColor DarkCyan
Write-Host "    OpenAPI:       http://localhost:$BACKEND_PORT/openapi/v1.json" -ForegroundColor DarkCyan
Write-Host "    Keycloak:      http://localhost:$($INFRA_PORTS.Keycloak)/admin (admin/admin123)" -ForegroundColor DarkCyan
Write-Host ""
Write-Host "  Logs folder:     $LOG_DIR" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Commands:" -ForegroundColor White
Write-Host "    .\dev.bat -Status       Check all services" -ForegroundColor DarkGray
Write-Host "    .\dev.bat -Logs         Open logs folder" -ForegroundColor DarkGray
Write-Host "    .\dev.bat -Kill         Stop all dev processes" -ForegroundColor DarkGray
Write-Host "    .\dev.bat -Clean        Clean all build artifacts" -ForegroundColor DarkGray
Write-Host "    .\dev.bat -FixKeycloak  Fix Keycloak issues" -ForegroundColor DarkGray
Write-Host "    .\dev.bat -Backend      Start backend only" -ForegroundColor DarkGray
Write-Host "    .\dev.bat -Frontend     Start frontend only" -ForegroundColor DarkGray
Write-Host ""
