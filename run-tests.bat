@echo off
REM ElysStay Testing Suite - Quick Run Script

setlocal enabledelayedexpansion

echo.
echo ===============================================
echo ElysStay Test Suite Runner
echo ===============================================
echo.

REM Parse command line arguments
if "%~1"=="" (
    set TEST_TYPE=all
) else (
    set TEST_TYPE=%~1
)

echo Test Type: !TEST_TYPE!
echo.

REM Route to correct handler
if /i "!TEST_TYPE!"=="all" goto run_all
if /i "!TEST_TYPE!"=="unit" goto run_unit
if /i "!TEST_TYPE!"=="integration" goto run_integration
if /i "!TEST_TYPE!"=="acceptance" goto run_acceptance
if /i "!TEST_TYPE!"=="coverage" goto run_coverage

goto unknown

:run_all
echo Running ALL tests (Unit + Integration + Acceptance)...
echo.
dotnet test --logger "console;verbosity=minimal"
goto end

:run_unit
echo Running UNIT tests only...
echo.
dotnet test Tests.Unit --logger "console;verbosity=minimal"
goto end

:run_integration
echo Running INTEGRATION tests only...
echo.
dotnet test Tests.Integration --logger "console;verbosity=minimal"
goto end

:run_acceptance
echo Running ACCEPTANCE (Cucumber) tests only...
echo.
dotnet test Tests.Acceptance --logger "console;verbosity=minimal"
goto end

:run_coverage
echo Running ALL tests with code coverage...
echo.
dotnet test --collect:"XPlat Code Coverage" --logger "console;verbosity=minimal" --results-directory "./coverage"
echo.
echo Coverage report generated in ./coverage/
goto end

:unknown
echo Unknown test type: !TEST_TYPE!
echo.
echo Usage: run-tests.bat [all^|unit^|integration^|acceptance^|coverage]
echo.
echo Examples:
echo   run-tests.bat              - Run all tests
echo   run-tests.bat unit         - Run unit tests only
echo   run-tests.bat integration  - Run integration tests only
echo   run-tests.bat acceptance   - Run acceptance tests only
echo   run-tests.bat coverage     - Run all tests with coverage report
exit /b 1

:end
if %ERRORLEVEL% equ 0 (
    echo.
    echo ===============================================
    echo Tests completed successfully!
    echo ===============================================
) else (
    echo.
    echo ===============================================
    echo Tests FAILED!
    echo ===============================================
    exit /b 1
)