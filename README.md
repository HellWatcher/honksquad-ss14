<div class="header" align="center">  
<img alt="Space Station 14" width="880" height="300" src="https://raw.githubusercontent.com/space-wizards/asset-dump/de329a7898bb716b9d5ba9a0cd07f38e61f1ed05/github-logo.svg">  
</div>

Honksquad is a downstream fork of [Space Station 14](https://github.com/space-wizards/space-station-14), a remake of SS13 built on the [Robust Toolbox](https://github.com/space-wizards/RobustToolbox) engine in C#.

## Links

<div class="header" align="center">

[Discord](https://discord.gg/honk) | [Upstream Repo](https://github.com/space-wizards/space-station-14) | [SS14 Docs](https://docs.spacestation14.com/) | [Steam](https://store.steampowered.com/app/1255460/Space_Station_14/)

</div>

## Contributing

We welcome contributions! Join the [Discord](https://discord.gg/honk) if you want to help or have questions.

Please follow the upstream [contribution guidelines](https://docs.spacestation14.com/en/general-development/codebase-info/pull-request-guidelines.html) for code style and PR expectations.

All fork-specific commits must be prefixed with `honksquad:` (e.g., `honksquad: feat: add new feature`). This keeps our changes clearly separated from upstream when syncing.

## AI-assisted contributions

AI-assisted contributions to code, YAML, and documentation are accepted, provided the contributor understands and can speak to the changes they submit. Low-effort, unreviewed dumps will be rejected like any other low-quality PR.

AI-generated artwork, sound files, and other creative assets are **not accepted**.

## Building

1. Clone this repo:

```shell
git clone https://github.com/HellWatcher/honksquad-ss14.git
```

2. Initialize submodules and load the engine:

```shell
cd honksquad-ss14
python RUN_THIS.py
```

3. Build:

```shell
dotnet build
```

[More detailed instructions on building the project.](https://docs.spacestation14.com/en/general-development/setup.html)

## License

honksquad-ss14 is a mixed-license project tracked per file with [REUSE](https://reuse.software). The `LICENSES/` directory holds the full text of every license in use, and `REUSE.toml` at the repo root maps each file to its license. Run `reuse lint` to check compliance.

- Code and content inherited from [Space Station 14](https://github.com/space-wizards/space-station-14) stays under the [MIT license](LICENSES/MIT.txt), the same as upstream.
- Code and content written for the fork (anything under a `RussStation` / `@RussStation` path, or a `*.Honk.cs` file) is [AGPL-3.0-or-later](LICENSES/AGPL-3.0-or-later.txt). The AGPL's network clause applies, so a running honksquad server owes its players the complete source of the version it is running.
- Assets (sprites, audio) are [CC-BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0/) unless their `meta.json` says otherwise. The `meta.json` beside each asset is the authoritative record of its license and copyright, for example the [metadata for a crowbar](https://github.com/space-wizards/space-station-14/blob/master/Resources/Textures/Objects/Tools/crowbar.rsi/meta.json).

> [!NOTE]
> Some assets are licensed under the non-commercial [CC-BY-NC-SA 3.0](https://creativecommons.org/licenses/by-nc-sa/3.0/) or similar non-commercial licenses and would need to be removed to use this project commercially.
