#!/usr/bin/env bash
# Local CI - replicates the GitHub Actions checks for PR validation.
# Usage: ./ci-local.sh [flags]
#
# Flags:
#   --skip-build    Skip the build step
#   --skip-tests    Skip both test suites
#   --skip-lint     Skip the YAML linter
#   --only-build    Run only the build step
#   --only-tests    Run only the test suites
#   --only-lint     Run only the YAML linter
set -euo pipefail

SKIP_BUILD=false
SKIP_TESTS=false
SKIP_LINT=false
ONLY_BUILD=false
ONLY_TESTS=false
ONLY_LINT=false

for arg in "$@"; do
    case $arg in
        --skip-build)  SKIP_BUILD=true ;;
        --skip-tests)  SKIP_TESTS=true ;;
        --skip-lint)   SKIP_LINT=true ;;
        --only-build)  ONLY_BUILD=true ;;
        --only-tests)  ONLY_TESTS=true ;;
        --only-lint)   ONLY_LINT=true ;;
        --help|-h)
            echo "Usage: ./ci-local.sh [--skip-build] [--skip-tests] [--skip-lint]"
            echo "                     [--only-build] [--only-tests] [--only-lint]"
            exit 0
            ;;
    esac
done

# --only flags override --skip flags: run only the specified step(s)
if $ONLY_BUILD || $ONLY_TESTS || $ONLY_LINT; then
    $ONLY_BUILD || SKIP_BUILD=true
    $ONLY_TESTS || SKIP_TESTS=true
    $ONLY_LINT  || SKIP_LINT=true
fi

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
DIM='\033[2m'
NC='\033[0m'

# Timing helpers
declare -A STEP_TIMES
TOTAL_START=$SECONDS

step_start() { STEP_START=$SECONDS; }
step_end() {
    local name="$1"
    local elapsed=$(( SECONDS - STEP_START ))
    STEP_TIMES["$name"]=$elapsed
}

pass() { echo -e "${GREEN}PASS${NC}: $1 ${DIM}(${2}s)${NC}"; }
fail() { echo -e "${RED}FAIL${NC}: $1 ${DIM}(${2}s)${NC}"; FAILED=true; }
skip() { echo -e "${YELLOW}SKIP${NC}: $1"; }

FAILED=false

echo "========================================"
echo "  Local CI - honksquad-ss14"
echo "========================================"
echo ""

# --- CRLF Check ---
echo "--- CRLF Check ---"
step_start
if git grep -rlI $'\r' -- '*.cs' '*.yml' '*.yaml' '*.csproj' > /dev/null 2>&1; then
    step_end "CRLF Check"
    fail "CRLF Check" "${STEP_TIMES["CRLF Check"]}"
    git grep -rlI $'\r' -- '*.cs' '*.yml' '*.yaml' '*.csproj' | head -10
else
    step_end "CRLF Check"
    pass "CRLF Check" "${STEP_TIMES["CRLF Check"]}"
fi
echo ""

# --- Build (DebugOpt) ---
if [ "$SKIP_BUILD" = false ]; then
    echo "--- Build (DebugOpt) ---"
    step_start
    if dotnet build --configuration DebugOpt --no-restore /m 2>&1 | tee /dev/stderr | grep -q "0 Error(s)"; then
        step_end "Build"
        pass "Build (DebugOpt)" "${STEP_TIMES["Build"]}"
    else
        step_end "Build"
        fail "Build (DebugOpt)" "${STEP_TIMES["Build"]}"
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
    step_start
    if dotnet test --no-build --configuration DebugOpt Content.Tests/Content.Tests.csproj -- NUnit.ConsoleOut=0 2>&1 | tee /dev/stderr | grep -q "Passed!"; then
        step_end "Unit Tests"
        pass "Content.Tests" "${STEP_TIMES["Unit Tests"]}"
    else
        step_end "Unit Tests"
        fail "Content.Tests" "${STEP_TIMES["Unit Tests"]}"
    fi
    echo ""

    echo "--- Content.IntegrationTests ---"
    step_start
    DOTNET_gcServer=1 dotnet test --no-build --configuration DebugOpt Content.IntegrationTests/Content.IntegrationTests.csproj -- NUnit.ConsoleOut=0 NUnit.MapWarningTo=Failed 2>&1 | tee /dev/stderr
    if [ "${PIPESTATUS[0]}" -eq 0 ]; then
        step_end "Integration Tests"
        pass "Content.IntegrationTests" "${STEP_TIMES["Integration Tests"]}"
    else
        step_end "Integration Tests"
        fail "Content.IntegrationTests" "${STEP_TIMES["Integration Tests"]}"
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
    step_start
    if dotnet run --project Content.YAMLLinter/Content.YAMLLinter.csproj --no-build 2>&1 | tee /dev/stderr; [ "${PIPESTATUS[0]}" -eq 0 ]; then
        step_end "YAML Linter"
        pass "YAML Linter" "${STEP_TIMES["YAML Linter"]}"
    else
        step_end "YAML Linter"
        fail "YAML Linter" "${STEP_TIMES["YAML Linter"]}"
    fi
    echo ""
else
    skip "YAML Linter"
    echo ""
fi

# --- Summary ---
TOTAL_ELAPSED=$(( SECONDS - TOTAL_START ))

echo "========================================"
if [ "$FAILED" = true ]; then
    echo -e "  ${RED}CI FAILED${NC}  (${TOTAL_ELAPSED}s total)"
else
    echo -e "  ${GREEN}CI PASSED${NC}  (${TOTAL_ELAPSED}s total)"
fi
echo ""

# Print timing breakdown
if [ ${#STEP_TIMES[@]} -gt 0 ]; then
    echo "  Timing:"
    for step in "CRLF Check" "Build" "Unit Tests" "Integration Tests" "YAML Linter"; do
        if [ -n "${STEP_TIMES[$step]+x}" ]; then
            printf "    %-25s %4ss\n" "$step" "${STEP_TIMES[$step]}"
        fi
    done
    echo ""
fi

if [ "$FAILED" = true ]; then
    exit 1
else
    exit 0
fi
