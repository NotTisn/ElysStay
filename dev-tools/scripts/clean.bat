@echo off
setlocal
REM ============================================================================
REM Nuclear Clean - Full Reset for ElysStay
REM ============================================================================

echo.
echo   WARNING: This will:
echo     - Kill ALL dev processes (backend + frontend)
echo     - Stop ALL Docker containers
echo     - Delete ALL build artifacts (bin/obj/.next)
echo     - Delete ALL dev logs
echo.

set /p confirm="   Continue? [y/N]: "
if /i not "%confirm%"=="y" (
    echo   Aborted.
    exit /b 0
)

set "SCRIPT_DIR=%~dp0"
set "TOOLS_DIR=%SCRIPT_DIR%.."
set "REPO_DIR=%SCRIPT_DIR%..\.."  
set "WORKSPACE_DIR=%SCRIPT_DIR%..\..\.."

echo.
echo [1/4] Killing dev processes...
for %%p in (5027 3000) do (
    for /f "tokens=5" %%a in ('netstat -ano 2^>nul ^| findstr ":%%p" ^| findstr "LISTENING"') do (
        echo   Stopping port %%p (PID %%a)
        taskkill /F /PID %%a >nul 2>&1
    )
)
timeout /t 2 /nobreak >nul

echo [2/4] Stopping Docker containers...
cd /d "%REPO_DIR%"
docker compose down 2>nul

echo [3/4] Deleting build artifacts...
for %%d in (API Application Domain Infrastructure) do (
    for %%t in (bin obj) do (
        if exist "%REPO_DIR%\%%d\%%t" (
            echo   ElysStay-be\%%d\%%t
            rd /s /q "%REPO_DIR%\%%d\%%t" 2>nul
        )
    )
)
if exist "%WORKSPACE_DIR%\ElysStay-fe\.next" (
    echo   ElysStay-fe\.next
    rd /s /q "%WORKSPACE_DIR%\ElysStay-fe\.next" 2>nul
)

echo [4/4] Clearing logs...
if exist "%TOOLS_DIR%\logs" rd /s /q "%TOOLS_DIR%\logs" 2>nul

echo.
echo   Nuclear clean complete. Run 'dev.bat' to rebuild.
echo.
