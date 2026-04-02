#!/usr/bin/env bash
# Local CI - replicates the GitHub Actions checks for PR validation.
# Usage: ./ci-local.sh [--skip-build] [--skip-tests] [--skip-lint]
set -euo pipefail

SKIP_BUILD=false
SKIP_TESTS=false
SKIP_LINT=false

for arg in "$@"; do
    case $arg in
        --skip-build) SKIP_BUILD=true ;;
        --skip-tests) SKIP_TESTS=true ;;
        --skip-lint)  SKIP_LINT=true ;;
        --help|-h)
            echo "Usage: ./ci-local.sh [--skip-build] [--skip-tests] [--skip-lint]"
            exit 0
            ;;
    esac
done

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

pass() { echo -e "${GREEN}PASS${NC}: $1"; }
fail() { echo -e "${RED}FAIL${NC}: $1"; FAILED=true; }
skip() { echo -e "${YELLOW}SKIP${NC}: $1"; }

FAILED=false

echo "========================================"
echo "  Local CI - honksquad-ss14"
echo "========================================"
echo ""

# --- CRLF Check ---
echo "--- CRLF Check ---"
if git grep -rlI $'\r' -- '*.cs' '*.yml' '*.yaml' '*.csproj' > /dev/null 2>&1; then
    fail "CRLF line endings found"
    git grep -rlI $'\r' -- '*.cs' '*.yml' '*.yaml' '*.csproj' | head -10
else
    pass "CRLF Check"
fi
echo ""

# --- Build (DebugOpt) ---
if [ "$SKIP_BUILD" = false ]; then
    echo "--- Build (DebugOpt) ---"
    if dotnet build --configuration DebugOpt --no-restore /m 2>&1 | tee /dev/stderr | grep -q "0 Error(s)"; then
        pass "Build (DebugOpt)"
    else
        fail "Build (DebugOpt)"
        exit 1
    fi
    echo ""
else
    skip "Build (DebugOpt)"
    echo ""
fi

# --- Unit Tests ---
if [ "$SKIP_TESTS" = false ]; then
    echo "--- Content.Tests ---"
    if dotnet test --no-build --configuration DebugOpt Content.Tests/Content.Tests.csproj -- NUnit.ConsoleOut=0 2>&1 | tee /dev/stderr | grep -q "Passed!"; then
        pass "Content.Tests"
    else
        fail "Content.Tests"
    fi
    echo ""

    echo "--- Content.IntegrationTests ---"
    DOTNET_gcServer=1 dotnet test --no-build --configuration DebugOpt Content.IntegrationTests/Content.IntegrationTests.csproj -- NUnit.ConsoleOut=0 NUnit.MapWarningTo=Failed 2>&1 | tee /dev/stderr
    if [ "${PIPESTATUS[0]}" -eq 0 ]; then
        pass "Content.IntegrationTests"
    else
        fail "Content.IntegrationTests"
    fi
    echo ""
else
    skip "Content.Tests"
    skip "Content.IntegrationTests"
    echo ""
fi

# --- YAML Linter ---
if [ "$SKIP_LINT" = false ]; then
    echo "--- YAML Linter ---"
    if dotnet run --project Content.YAMLLinter/Content.YAMLLinter.csproj --no-build 2>&1 | tee /dev/stderr | grep -q "0 errors"; then
        pass "YAML Linter"
    else
        fail "YAML Linter"
    fi
    echo ""
else
    skip "YAML Linter"
    echo ""
fi

# --- Summary ---
echo "========================================"
if [ "$FAILED" = true ]; then
    echo -e "  ${RED}CI FAILED${NC}"
    exit 1
else
    echo -e "  ${GREEN}CI PASSED${NC}"
    exit 0
fi
