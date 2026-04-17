# Branching Strategy

This document describes the branching strategy for the HonkSquad SS14 fork, designed to cleanly manage upstream (Wizden) syncs and fork-specific development.

## The upstream rule

**`wizden/stable` is the only upstream source for this fork.** Never merge `wizden/master`, `upstream/master`, or any non-stable upstream branch into `staging`, `release`, or any branch destined for `release`. The `release-base-integrity` CI job (`.github/workflows/check-release-base.yml`) enforces this, blocking any push or PR whose merge-base with `origin/upstream/stable` is not itself reachable from `wizden/stable`.

Every fork-authored commit must use the `honksquad:` subject prefix. The `commit-prefix` CI job enforces this automatically: commits reachable from `origin/upstream/stable` are exempt (upstream), everything else must carry the prefix.

If release drifts onto a non-stable base, the recovery procedure is a full resync: reclassify every commit as fork-preserve or upstream-drop, replay the fork commits onto a clean `wizden/stable` base, force-push release, and rebase all open PRs with `git rebase --onto origin/release <pre-resync-tag>`.

## Cross-repository PRs

External contributors submit PRs from their own forks. When rebasing these PRs (e.g., during an upstream sync), push to the **contributor's fork**, not `origin`. Pushing to `origin` creates a dangling mirror branch that does not update the PR.

Use `gh pr checkout <N>` to fetch the contributor's branch, rebase, then force-push back to their remote. You will need to add their fork as a named remote and fetch before `--force-with-lease` works (the `gh`-configured URL is not a tracked remote).

## Branch Overview

```
upstream/stable   <- mirrors wizden/stable (read-only)
       |
       v
    release        <- our deployable branch (upstream + tested fork additions)
       |
       ├── feat/X          <- feature branches (PR targets release)
       │    ├── fix/123-bug-description   <- sub-branches (PR targets feat/X)
       │    └── fix/124-other-bug
       ├── fix/Y
       └── feat/Z
```

## Remotes

| Remote   | URL                                         | Purpose  |
| -------- | ------------------------------------------- | -------- |
| `origin` | `github.com/HellWatcher/honksquad-ss14`     | Our fork |
| `wizden` | `github.com/space-wizards/space-station-14` | Upstream |

To add the wizden remote locally:

```bash
git remote add wizden https://github.com/space-wizards/space-station-14.git
```

## Branches

### `upstream/stable`

- **Purpose:** Clean mirror of Wizden's `stable` branch.
- **Updates:** Force-pushed to match `wizden/stable` during planned upstream syncs. Never receives fork commits.
- **Who updates it:** Maintainers only.
- **Protected:** Yes. No direct pushes except during sync (temporarily allow force-push).

### `release`

- **Purpose:** The primary deployable branch. Contains all tested fork additions on top of the upstream base.
- **Updates:** Receives merges from:
    1. `upstream/stable` (when syncing to a new Wizden stable)
    2. Feature/fix PRs (after review and testing)
- **Protected:** Yes. PRs required.

### `staging` (created as needed)

- **Purpose:** Disposable branch for integration testing multiple in-progress features together.
- **Lifecycle:** Created from `release` + cherry-picked PRs under test. Deleted when no longer needed.
- **Important:** Never base new work off this branch.

### Feature/Fix Branches

- **Naming:** `feat/<description>` or `fix/<description>`
- **Based on:** Always branch from `release`.
- **PRs target:** `release`.
- **Deleted after merge.**

### Sub-Branches (Bug Fixes Within a Feature)

When a feature PR has bugs or follow-up work tracked as issues, each fix gets its own branch off the feature branch, not off `release`.

- **Naming:** `fix/<issue-number>-<short-description>` (e.g., `fix/305-autosurgeon-access`)
- **Based on:** The parent feature branch (e.g., `feat/cybernetic-organs`).
- **PRs target:** The parent feature branch, not `release`.
- **Deleted after merge into the parent branch.**

This keeps each fix reviewable in isolation while the parent feature PR stays as the rollup into `release`. The parent PR accumulates all merged sub-branch work automatically.

## Workflows

### Starting New Work

```bash
git fetch origin release
git checkout -b feat/my-feature origin/release
# ... make changes, commit, push ...
# Open PR targeting release
```

### Fixing Bugs in a Feature Branch

When a feature PR has tracked bugs, create a sub-branch per issue:

```bash
git fetch origin feat/my-feature
git checkout -b fix/123-bug-description origin/feat/my-feature
# ... fix the bug, commit, push ...
# Open PR targeting feat/my-feature (not release)
gh pr create --base feat/my-feature --repo HellWatcher/honksquad-ss14
```

After the fix PR merges, the parent feature branch picks up the changes. Repeat for each bug. The parent feature PR into `release` is the final integration point.

### Syncing Upstream (Wizden Update)

1. **Fetch and update the upstream mirror:**

    ```bash
    git fetch wizden stable
    git checkout upstream/stable
    git reset --hard wizden/stable
    # Temporarily disable force-push protection, then:
    git push --force origin upstream/stable
    # Re-enable protection
    ```

2. **Merge upstream into release:**

    ```bash
    git checkout release
    git merge upstream/stable
    # Resolve conflicts
    git push origin release
    ```

3. **Rebase open feature branches** (if needed):
    ```bash
    git checkout feat/my-feature
    git rebase origin/release
    git push --force-with-lease origin feat/my-feature
    ```

### Handling Upstream Conflicts

- **Namespace fork code.** Keep fork-specific files in subdirectories (e.g., `RussStation/`, `HonkSquad/`) and prefix component names to minimize collisions.
- **Mark fork changes.** Use `//HONK START` / `//HONK END` block markers around inline changes to upstream files. Inline `// honksquad:` tails are not accepted by the HONK audit; see `CONTRIBUTING.md`.
- **Keep upstream files clean.** Prefer extending via new components/systems rather than editing existing ones.
- **Sync regularly.** Small, frequent syncs are easier than large, infrequent ones.

## Branch Protection Rules

| Branch             | Direct Push | Force Push                | PR Required |
| ------------------ | ----------- | ------------------------- | ----------- |
| `upstream/stable`  | No          | Maintainers (during sync) | No          |
| `release`          | No          | No                        | Yes         |
| `staging`          | Yes         | Yes                       | No          |
| `feat/*` / `fix/*` | Author      | Yes (with lease)          | N/A         |
