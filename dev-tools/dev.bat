@echo off
REM ============================================================================
REM ElysStay Dev Mode - Simple wrapper for dev-mode.ps1
REM ============================================================================
REM
REM Usage:
REM   dev.bat              Start all (infra + backend + frontend)
REM   dev.bat -Watch       Start all + error watcher
REM   dev.bat -Status      Show service status
REM   dev.bat -Logs        Open logs folder
REM   dev.bat -ClearLogs   Clear logs only (no restart)
REM   dev.bat -Kill        Kill all dev processes
REM   dev.bat -Clean       Clean all build artifacts (use after branch switch!)
REM   dev.bat -Infra       Start infrastructure only (Docker)
REM   dev.bat -InfraDown   Stop infrastructure (Docker)
REM   dev.bat -FixKeycloak Fix Keycloak issues
REM   dev.bat -Backend     Start backend only
REM   dev.bat -Frontend    Start frontend only
REM
REM IMPORTANT: After switching git branches, run `dev -Kill` then `dev -Clean`!
REM
REM ============================================================================

powershell -ExecutionPolicy Bypass -File "%~dp0scripts\dev-mode.ps1" %*
