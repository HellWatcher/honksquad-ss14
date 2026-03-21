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

## AI-assisted contributions

AI-assisted contributions to code, YAML, and documentation are accepted, provided the contributor understands and can speak to the changes they submit. Low-effort, unreviewed dumps will be rejected like any other low-quality PR.

AI-generated artwork, sound files, and other creative assets are **not accepted**.

## Building

1. Clone this repo:

```shell
git clone https://github.com/HellWatcher/space-station-14.git
```

2. Initialize submodules and load the engine:

```shell
cd space-station-14
python RUN_THIS.py
```

3. Build:

```shell
dotnet build
```

[More detailed instructions on building the project.](https://docs.spacestation14.com/en/general-development/setup.html)

## License

All code for the content repository is licensed under the [MIT license](https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT).

Most assets are licensed under [CC-BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0/) unless stated otherwise. Assets have their license and copyright specified in the metadata file. For example, see the [metadata for a crowbar](https://github.com/space-wizards/space-station-14/blob/master/Resources/Textures/Objects/Tools/crowbar.rsi/meta.json).

> [!NOTE]
> Some assets are licensed under the non-commercial [CC-BY-NC-SA 3.0](https://creativecommons.org/licenses/by-nc-sa/3.0/) or similar non-commercial licenses and will need to be removed if you wish to use this project commercially.
