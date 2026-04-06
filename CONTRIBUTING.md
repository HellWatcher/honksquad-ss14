# Contributing to Honksquad

Honksquad is a downstream fork of [Space Station 14](https://github.com/space-wizards/space-station-14). We rebase on upstream regularly, so most of the rules here exist to keep that process painless.

## Setup

1. Clone the repo and run `python RUN_THIS.py` to initialize submodules and the engine.
2. Build with `dotnet build`.
3. Start the server with `./runserver.sh` and the client with `./runclient.sh`.

## Branching

All work targets the `release` branch. See [BRANCHING.md](BRANCHING.md) for the full strategy.

```bash
git fetch origin release
git checkout -b feat/my-feature origin/release
# Make changes, commit, push, open PR targeting release
```

Branch names use `feat/<description>` or `fix/<description>`. All fork-specific commits start with `honksquad:` followed by a conventional commit type:

```
honksquad: feat: add new feature
honksquad: fix: resolve null reference in carry system
honksquad: refactor: extract helper for item weight calculation
```

## The upstream rule

Don't modify upstream files unless you have to. Every upstream file we touch is a potential merge conflict on the next rebase.

Instead, add new files (components, systems, events) and use ECS event subscriptions to hook into existing behavior. When you must edit an upstream file (adding a component to a prototype YAML, for example), keep the diff small.

### Marking your changes

Wrap fork changes in upstream C# files with marker comments so they're easy to find during rebase:

```csharp
//HONK START - Brief description
using Some.New.Namespace;
//HONK END
```

For YAML, use `#` comments:

```yaml
# HONK START - Brief description
- type: SomeComponent
  someField: value
# HONK END
```

### YAML indentation

Upstream YAML uses a specific indentation style. Don't reformat it. Match whatever the surrounding lines do, and only add or remove the lines you need. Formatters will fight you on this, so double-check your diffs.

### Where new files go

Fork-specific files live under `@RussStation` prefixed directories so they never collide with upstream paths:

- `Resources/Prototypes/@RussStation/` for prototype YAML
- `Resources/Textures/@RussStation/` for sprites and RSIs
- `Resources/Audio/@RussStation/` for sound files
- `Content.Shared/RussStation/`, `Content.Server/RussStation/`, `Content.Client/RussStation/` for C#

## Bug fixes within feature PRs

When a feature PR has bugs tracked as issues, each fix gets its own branch off the feature branch (not off `release`). The fix PR targets the feature branch, not `release`.

```
feat/my-feature        <- parent PR targets release
  ├── fix/123-some-bug   <- PR targets feat/my-feature
  └── fix/124-other-bug  <- PR targets feat/my-feature
```

This keeps fixes reviewable in isolation. The parent feature PR rolls everything up into `release`. See [BRANCHING.md](BRANCHING.md) for the full workflow.

## Pull requests

Use the [PR template](.github/PULL_REQUEST_TEMPLATE.md) and fill in all sections. Follow the upstream [PR guidelines](https://docs.spacestation14.com/en/general-development/codebase-info/pull-request-guidelines.html).

Player-facing changes need a `:cl:` changelog entry:

```
:cl:
- add: Added fun!
- remove: Removed fun!
- tweak: Changed fun!
- fix: Fixed fun!
```

## Testing

Run tests before submitting:

- `dotnet test Content.Tests` for unit tests
- `dotnet test Content.IntegrationTests` for integration tests
- `./ci-local.sh` to replicate the full CI pipeline locally (build, tests, YAML lint)

Integration tests treat YAML mapping warnings as failures, so fix those before pushing.

## AI-assisted contributions

AI-assisted contributions to code, YAML, and documentation are accepted, as long as you understand and can explain the changes you're submitting. Low-effort, unreviewed dumps get rejected like any other low-quality PR.

AI-generated artwork, sound files, and other creative assets are not accepted.

## Questions?

Join the [Discord](https://discord.gg/honk).
