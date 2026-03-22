#!/bin/bash
# ElysStay Testing Suite - Quick Run Script for Linux/Mac

TEST_TYPE="${1:-all}"

echo ""
echo "==============================================="
echo "ElysStay Test Suite Runner"
echo "==============================================="
echo ""
echo "Test Type: $TEST_TYPE"
echo ""

case "$TEST_TYPE" in
    "all")
        echo "Running ALL tests (Unit + Integration + Acceptance)..."
        echo ""
        dotnet test --logger "console;verbosity=minimal"
        ;;
    "unit")
        echo "Running UNIT tests only..."
        echo ""
        dotnet test Tests.Unit --logger "console;verbosity=minimal"
        ;;
    "integration")
        echo "Running INTEGRATION tests only..."
        echo ""
        dotnet test Tests.Integration --logger "console;verbosity=minimal"
        ;;
    "acceptance")
        echo "Running ACCEPTANCE (Cucumber) tests only..."
        echo ""
        dotnet test Tests.Acceptance --logger "console;verbosity=minimal"
        ;;
    "coverage")
        echo "Running ALL tests with code coverage..."
        echo ""
        dotnet test --collect:"XPlat Code Coverage" --logger "console;verbosity=minimal" --results-directory "./coverage"
        echo ""
        echo "Coverage report generated in ./coverage/"
        ;;
    *)
        echo "Unknown test type: $TEST_TYPE"
        echo ""
        echo "Usage: ./run-tests.sh [all|unit|integration|acceptance|coverage]"
        echo ""
        echo "Examples:"
        echo "  ./run-tests.sh              - Run all tests"
        echo "  ./run-tests.sh unit         - Run unit tests only"
        echo "  ./run-tests.sh integration  - Run integration tests only"
        echo "  ./run-tests.sh acceptance   - Run acceptance tests only"
        echo "  ./run-tests.sh coverage     - Run all tests with coverage report"
        echo ""
        exit 1
        ;;
esac

if [ $? -eq 0 ]; then
    echo ""
    echo "==============================================="
    echo "Tests completed successfully!"
    echo "==============================================="
else
    echo ""
    echo "==============================================="
    echo "Tests FAILED!"
    echo "==============================================="
    exit 1
fi
