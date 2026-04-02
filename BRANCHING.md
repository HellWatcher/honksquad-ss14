# Branching Strategy

This document describes the branching strategy for the HonkSquad SS14 fork, designed to cleanly manage upstream (Wizden) syncs and fork-specific development.

## Branch Overview

```
upstream/stable   <- mirrors wizden/stable (read-only)
       |
       v
    release        <- our deployable branch (upstream + tested fork additions)
       |
       ├── feat/X     <- individual feature/fix branches (PR targets release)
       ├── fix/Y
       └── feat/Z
```

## Remotes

| Remote | URL | Purpose |
|--------|-----|---------|
| `origin` | `github.com/HellWatcher/honksquad-ss14` | Our fork |
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

## Workflows

### Starting New Work

```bash
git fetch origin release
git checkout -b feat/my-feature origin/release
# ... make changes, commit, push ...
# Open PR targeting release
```

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
- **Mark fork changes.** Use `// honksquad:` comments near inline changes to upstream files so they're easy to find during conflict resolution.
- **Keep upstream files clean.** Prefer extending via new components/systems rather than editing existing ones.
- **Sync regularly.** Small, frequent syncs are easier than large, infrequent ones.

## Branch Protection Rules

| Branch | Direct Push | Force Push | PR Required |
|--------|------------|------------|-------------|
| `upstream/stable` | No | Maintainers (during sync) | No |
| `release` | No | No | Yes |
| `staging` | Yes | Yes | No |
| `feat/*` / `fix/*` | Author | Yes (with lease) | N/A |
