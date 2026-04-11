# Canister Gas Table

Human-readable companion to `gases.json`. Shows the template and color pair used
to bake each gas canister, plus which SS14 canister entity consumes it.

Colors come from the SS13 russ-station source at
`code/modules/atmospherics/machinery/portable/canister.dm`. Templates mirror the
greyscale JSON configs in `code/datums/greyscale/json_configs/`.

## Upstream base gases (wired in this PR)

| Gas            | Template      | Primary   | Secondary | SS14 entity             |
| -------------- | ------------- | --------- | --------- | ----------------------- |
| air            | default       | `#c6c0b5` |           | `AirCanister`           |
| oxygen         | stripe        | `#2786e5` | `#e8fefe` | `OxygenCanister`        |
| nitrogen       | double_stripe | `#e9ff5c` | `#f4fce8` | `NitrogenCanister`      |
| carbon_dioxide | double_stripe | `#4e4c48` | `#eaeaea` | `CarbonDioxideCanister` |
| plasma         | hazard        | `#f62800` | `#ffee00` | `PlasmaCanister`        |
| tritium        | hazard        | `#3fcd40` | `#ffee00` | `TritiumCanister`       |
| nitrous_oxide  | double_stripe | `#c63e3b` | `#f7d5d3` | `NitrousOxideCanister`  |
| frezon         | double_stripe | `#6696ee` | `#fefb30` | `FrezonCanister`        |
| water_vapor    | double_stripe | `#4c4e4d` | `#f7d5d3` | `WaterVaporCanister`    |
| ammonia        | double_stripe | `#a8c84e` | `#e9ff5c` | `AmmoniaCanister`       |
| storage        | default       | `#6b6b80` |           | `StorageCanister`       |

Ammonia has no SS13 equivalent; palette is invented to match the SS13 look
(yellow-green double stripe, evoking the upstream `greenys` sprite). Storage
uses the authentic SS13 generic canister color from `canister.dm`.

## Fork-only gases (staged for PR 365)

| Gas           | Template      | Primary   | Secondary | SS14 entity            |
| ------------- | ------------- | --------- | --------- | ---------------------- |
| antinoblium   | double_stripe | `#333333` | `#fefb30` | `AntinobliumCanister`  |
| bz            | double_stripe | `#9b5d7f` | `#d0d2a0` | `BzCanister`           |
| halon         | double_stripe | `#9b5d7f` | `#368bff` | `HalonCanister`        |
| healium       | double_stripe | `#009823` | `#ff0e00` | `HealiumCanister`      |
| helium        | double_stripe | `#9b5d7f` | `#368bff` | `HeliumCanister`       |
| hydrogen      | double_stripe | `#eaeaea` | `#be3455` | `HydrogenCanister`     |
| miasma        | double_stripe | `#009823` | `#f7d5d3` | `MiasmaCanister`       |
| nitrium       | default       | `#7b4732` |           | `NitriumCanister`      |
| pluoxium      | default       | `#2786e5` |           | `PluoxiumCanister`     |
| proto_nitrate | double_stripe | `#008200` | `#33cc33` | `ProtoNitrateCanister` |

## Templates

| Name            | Colors | Layer order                                                                 |
| --------------- | ------ | --------------------------------------------------------------------------- |
| `default`       | 1      | base + add + multiply + outline + lights                                    |
| `stripe`        | 2      | default layers + `stripe` overlay (color 2)                                 |
| `double_stripe` | 2      | default layers + `double_stripe` overlay (color 2) + `double_stripe_shader` |
| `hazard`        | 2      | default layers + `hazard_stripes` overlay (color 2)                         |
