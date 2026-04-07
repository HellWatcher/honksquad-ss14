#!/usr/bin/env bash
# Checks for prototype ID collisions between fork (@RussStation) and upstream prototypes.
# Intentional overrides are valid but should be documented.
#
# Usage: Tools/prototype_id_check.sh
#
# In GitHub Actions, emits ::warning annotations.
# Locally, prints plain warnings.

set -euo pipefail

FORK_DIR="Resources/Prototypes/@RussStation"
UPSTREAM_DIR="Resources/Prototypes"

warn() {
    local msg="$1"
    if [ "${GITHUB_ACTIONS:-}" = "true" ]; then
        echo "::warning::${msg}"
    else
        echo "  WARN: ${msg}"
    fi
}

if [ ! -d "$FORK_DIR" ]; then
    echo "No fork prototypes directory found at ${FORK_DIR}"
    exit 0
fi

# Extract prototype IDs from fork YAML files
# Matches lines like:  id: SomeThing  or  id: "SomeThing"
FORK_IDS=$(grep -rh '^\s*id:\s*' "$FORK_DIR" --include='*.yml' --include='*.yaml' 2>/dev/null \
    | sed "s/.*id:\s*['\"]*//" | sed "s/['\"].*$//" | sed 's/\s*$//' | sort -u)

if [ -z "$FORK_IDS" ]; then
    echo "No prototype IDs found in ${FORK_DIR}"
    exit 0
fi

TOTAL=$(echo "$FORK_IDS" | wc -l)
COLLISIONS=0

echo "Checking ${TOTAL} fork prototype IDs for upstream collisions..."
echo ""

while IFS= read -r id; do
    [ -z "$id" ] && continue

    # Search upstream prototypes (exclude fork directory)
    UPSTREAM_MATCH=$(grep -rl "^\s*id:\s*['\"]*${id}['\"]* *$" "$UPSTREAM_DIR" \
        --include='*.yml' --include='*.yaml' --exclude-dir='@RussStation' 2>/dev/null | head -1 || true)

    if [ -n "$UPSTREAM_MATCH" ]; then
        FORK_MATCH=$(grep -rl "^\s*id:\s*['\"]*${id}['\"]* *$" "$FORK_DIR" \
            --include='*.yml' --include='*.yaml' 2>/dev/null | head -1 || true)
        warn "ID collision: '${id}' in ${FORK_MATCH} shadows ${UPSTREAM_MATCH}"
        ((COLLISIONS++)) || true
    fi
done <<< "$FORK_IDS"

echo ""
echo "Checked ${TOTAL} fork prototype ID(s), found ${COLLISIONS} collision(s) with upstream."

if [ "$COLLISIONS" -gt 0 ]; then
    echo ""
    echo "Collisions may be intentional overrides. If so, add a comment in the fork YAML:"
    echo "  # Intentional override: <reason>"
fi
