# Honksquad Contributing Guidelines

Thanks for contributing to Honksquad, a downstream fork of [Space Station 14](https://github.com/space-wizards/space-station-14).

## Code Style & PR Guidelines

Follow the upstream [codebase conventions](https://docs.spacestation14.com/en/general-development/codebase-info/codebase-organization.html) and [PR guidelines](https://docs.spacestation14.com/en/general-development/codebase-info/pull-request-guidelines.html).

## Fork-Specific Rules

- **Commit prefix:** All fork-specific commits must start with `honksquad:` (e.g., `honksquad: feat: add new feature`).
- **Avoid modifying upstream files** whenever possible. Add new features in new files using ECS event subscriptions.
- When upstream files must be touched, wrap changes with `//HONK START` / `//HONK END` marker comments (or `# HONK START` / `# HONK END` for YAML).
- New files go under `@RussStation` prefixed directories (`Resources/Prototypes/@RussStation/`, etc.).

## AI-Assisted Contributions

AI-assisted contributions to code, YAML, and documentation are accepted, provided the contributor understands and can speak to the changes they submit. Low-effort, unreviewed dumps will be rejected like any other low-quality PR.

AI-generated artwork, sound files, and other creative assets are **not accepted**.

## Getting Help

Join the [Discord](https://discord.gg/honk) if you want to help or have questions.
