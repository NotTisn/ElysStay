@echo off
setlocal
REM ============================================================================
REM Restart Single ElysStay Service
REM ============================================================================
REM Usage: restart.bat <backend|frontend>
REM ============================================================================

if "%~1"=="" (
    echo.
    echo   Usage: restart.bat ^<service^>
    echo.
    echo   Services:
    echo     backend      .NET API on port 5027
    echo     frontend     Next.js on port 3000
    echo.
    exit /b 1
)

set "SERVICE=%~1"
set "SCRIPT_DIR=%~dp0"

if /i "%SERVICE%"=="backend" (
    echo   Stopping backend on port 5027...
    for /f "tokens=5" %%a in ('netstat -ano 2^>nul ^| findstr ":5027" ^| findstr "LISTENING"') do (
        taskkill /F /PID %%a >nul 2>&1
    )
    timeout /t 2 /nobreak >nul
    echo   Starting backend...
    powershell -ExecutionPolicy Bypass -File "%SCRIPT_DIR%dev-mode.ps1" -Backend
    exit /b 0
)

if /i "%SERVICE%"=="frontend" (
    echo   Stopping frontend on port 3000...
    for /f "tokens=5" %%a in ('netstat -ano 2^>nul ^| findstr ":3000" ^| findstr "LISTENING"') do (
        taskkill /F /PID %%a >nul 2>&1
    )
    timeout /t 2 /nobreak >nul
    echo   Starting frontend...
    powershell -ExecutionPolicy Bypass -File "%SCRIPT_DIR%dev-mode.ps1" -Frontend
    exit /b 0
)

echo   Unknown service: %SERVICE%
echo   Valid: backend, frontend
exit /b 1
