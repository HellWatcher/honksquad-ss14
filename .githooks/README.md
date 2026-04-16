# Git hooks

Repository-tracked git hooks. One-time setup per clone:

```
git config core.hooksPath .githooks
```

## `pre-commit`

Rejects commits that make whitespace-only changes to upstream YAML files.
See `CLAUDE.md` § "YAML Formatting in Upstream Files".

Upstream YAML is anything under `Resources/Prototypes/` that isn't under
`@RussStation/` and isn't a map file under `Resources/Maps/`.

Bypass for intentional upstream-sync merges: `git commit --no-verify`.
