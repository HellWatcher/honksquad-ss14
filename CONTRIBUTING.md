# Honksquad Contributing Guidelines

Thanks for contributing to Honksquad, a downstream fork of [Space Station 14](https://github.com/space-wizards/space-station-14).

## Getting Started

1. Clone the repo and run `python RUN_THIS.py` to initialize submodules and the engine.
2. Build with `dotnet build`.
3. Start the server with `./runserver.sh` and the client with `./runclient.sh`.
4. Run tests with `dotnet test Content.IntegrationTests` and `dotnet test Content.Tests`.

## Branching & Workflow

All work targets the `release` branch. See [BRANCHING.md](BRANCHING.md) for the full strategy.

```bash
git fetch origin release
git checkout -b feat/my-feature origin/release
# Make changes, commit, push
# Open PR targeting release
```

- **Feature branches:** `feat/<description>`
- **Fix branches:** `fix/<description>`
- **Commit prefix:** All fork-specific commits must start with `honksquad:` followed by a conventional commit type.

    ```
    honksquad: feat: add new feature
    honksquad: fix: resolve null reference in carry system
    honksquad: refactor: extract helper for item weight calculation
    ```

## Code Style

Enforced via `.editorconfig`:

- 4 spaces indent (2 for XML, YAML, and csproj)
- File-scoped namespaces (`namespace Foo;`)
- Allman brace style (braces on new line)
- `var` everywhere
- No `this.` qualification
- Private fields: `_camelCase`
- Max line length: 120 characters

Follow the upstream [codebase conventions](https://docs.spacestation14.com/en/general-development/codebase-info/codebase-organization.html) for anything not covered here.

## Fork-Specific Rules

### Avoid Modifying Upstream Files

This fork regularly rebases on upstream. Every upstream file touched creates a potential merge conflict.

- Add new features in **new files** (new components, systems, events) rather than editing existing upstream code.
- Use ECS event subscriptions and hooks to extend behavior without modifying upstream systems.
- When upstream files _must_ be touched (e.g., adding a component to a prototype YAML), keep changes minimal and isolated.

### HONK Marker Comments

When you must modify upstream C# files, wrap all fork changes with marker comments:

```csharp
//HONK START - Brief description
using Some.New.Namespace;
//HONK END
```

For YAML:

```yaml
# HONK START - Brief description
- type: SomeComponent
  someField: value
# HONK END
```

### YAML Formatting in Upstream Files

Never reformat upstream YAML. Match the existing file's indentation exactly. Upstream uses a specific style where sequence items sit at the same indent level as the parent key, not indented beneath it. See CLAUDE.md for examples. Only add or remove the lines you need.

### New File Locations

Fork-specific files go under `@RussStation` prefixed directories to avoid upstream collisions:

- `Resources/Prototypes/@RussStation/` for new prototype YAML
- `Resources/Textures/@RussStation/` for new sprites and RSIs
- `Resources/Audio/@RussStation/` for new sound files
- `Content.Shared/RussStation/`, `Content.Server/RussStation/`, `Content.Client/RussStation/` for new C# code

## Pull Requests

Use the [PR template](.github/PULL_REQUEST_TEMPLATE.md) and fill in all sections (About the PR, Why / Balance, Technical details, Media, Requirements, Breaking changes, Changelog).

Follow the upstream [PR guidelines](https://docs.spacestation14.com/en/general-development/codebase-info/pull-request-guidelines.html).

### Changelog

Player-facing changes need a `:cl:` changelog entry:

```
:cl:
- add: Added fun!
- remove: Removed fun!
- tweak: Changed fun!
- fix: Fixed fun!
```

### Labels

Apply appropriate labels to your PR after creating it. Maintainers may adjust labels during review.

## Testing

- **Unit tests:** `dotnet test Content.Tests`
- **Integration tests:** `dotnet test Content.IntegrationTests`
- **Single test:** `dotnet test Content.Tests --filter "FullyQualifiedName~MyTestClass"`
- **Local CI:** `./ci-local.sh` runs build, tests, and YAML linting

Run tests before submitting your PR. Integration tests treat YAML mapping warnings as failures.

## AI-Assisted Contributions

AI-assisted contributions to code, YAML, and documentation are accepted, provided the contributor understands and can speak to the changes they submit. Low-effort, unreviewed dumps will be rejected like any other low-quality PR.

AI-generated artwork, sound files, and other creative assets are **not accepted**.

## Getting Help

Join the [Discord](https://discord.gg/honk) if you want to help or have questions.
