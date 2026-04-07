#!/usr/bin/env bash
# Audits modified upstream files for HONK marker comments.
# Helps catch unmarked fork changes that will be hard to find during rebase.
#
# Usage: Tools/honk_marker_audit.sh [base_ref]
#   base_ref: git ref to diff against (default: origin/release)
#
# In GitHub Actions, emits ::warning annotations.
# Locally, prints plain warnings.

set -euo pipefail

BASE_REF="${1:-origin/release}"

# Fork-specific path patterns (never need HONK markers)
FORK_PATTERNS=(
    "@RussStation"
    "RussStation/"
    ".github/workflows/honk-"
    ".github/workflows/pr-body"
    ".github/workflows/prototype-id"
    ".github/workflows/check-upstream"
    ".github/workflows/labeler-size"
    ".github/labeler.yml"
    "ci-local.sh"
    "deploy.sh"
    "CLAUDE.md"
    "BRANCHING.md"
    "CONTRIBUTING.md"
    ".claude/"
)

is_fork_file() {
    local file="$1"
    for pattern in "${FORK_PATTERNS[@]}"; do
        if [[ "$file" == *"$pattern"* ]]; then
            return 0
        fi
    done
    return 1
}

warn() {
    local file="$1"
    local msg="$2"
    if [ "${GITHUB_ACTIONS:-}" = "true" ]; then
        echo "::warning file=${file}::${msg}"
    else
        echo "  WARN: ${file} -- ${msg}"
    fi
}

# Resolve base ref
if ! git rev-parse "${BASE_REF}" >/dev/null 2>&1; then
    echo "Cannot resolve base ref: ${BASE_REF}"
    exit 1
fi

MODIFIED_FILES=$(git diff --name-only --diff-filter=d "${BASE_REF}"...HEAD 2>/dev/null \
    || git diff --name-only --diff-filter=d "${BASE_REF}" HEAD)

if [ -z "$MODIFIED_FILES" ]; then
    echo "No modified files found."
    exit 0
fi

WARNINGS=0
CHECKED=0

echo "Checking upstream files for HONK markers (base: ${BASE_REF})..."
echo ""

while IFS= read -r file; do
    # Skip fork-specific files
    if is_fork_file "$file"; then
        continue
    fi

    # Skip files that no longer exist
    if [ ! -f "$file" ]; then
        continue
    fi

    case "$file" in
        *.cs)
            ((CHECKED++)) || true
            if ! grep -q "HONK START\|HONK END" "$file" 2>/dev/null; then
                warn "$file" "Upstream C# file modified without HONK markers"
                ((WARNINGS++)) || true
            fi
            ;;
        *.yml|*.yaml)
            ((CHECKED++)) || true
            if ! grep -q "HONK START\|HONK END" "$file" 2>/dev/null; then
                warn "$file" "Upstream YAML file modified without HONK markers"
                ((WARNINGS++)) || true
            fi
            ;;
    esac
done <<< "$MODIFIED_FILES"

echo ""
echo "Checked ${CHECKED} upstream file(s), found ${WARNINGS} without HONK markers."

if [ "$WARNINGS" -gt 0 ]; then
    echo ""
    echo "Wrap fork changes with marker comments for easier rebase tracking:"
    echo "  C#:   //HONK START ... //HONK END"
    echo "  YAML: # HONK START ... # HONK END"
fi
