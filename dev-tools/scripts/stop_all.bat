@echo off
setlocal
REM ============================================================================
REM Stop All ElysStay Dev Services - By Known Ports
REM ============================================================================
REM Usage: stop_all.bat [--docker]
REM ============================================================================

echo.
echo   Stopping ElysStay Services...
echo.

set "STOP_DOCKER=0"
if /i "%~1"=="--docker" set "STOP_DOCKER=1"

REM Kill services by known ports (backend 5027, frontend 3000)
for %%p in (5027 3000) do (
    for /f "tokens=5" %%a in ('netstat -ano 2^>nul ^| findstr ":%%p" ^| findstr "LISTENING"') do (
        echo   Stopping port %%p (PID %%a)
        taskkill /F /PID %%a >nul 2>&1
    )
)

if "%STOP_DOCKER%"=="1" (
    echo.
    echo   Stopping Docker containers...
    cd /d "%~dp0..\.."
    docker compose down 2>nul
)

echo.
echo   Done.
echo.
