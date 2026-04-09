# Fork CVars

Configuration variables added by the Honksquad fork. These can be set in the server config or changed at runtime via the `cvar` command.

## Economy

| CVar                               | Type  | Default | Scope  | Description                                                |
| ---------------------------------- | ----- | ------- | ------ | ---------------------------------------------------------- |
| `economy.default_starting_balance` | int   | 250     | Server | Starting balance granted to players on spawn (spesos)      |
| `economy.payroll_interval`         | float | 300     | Server | Seconds between payroll deposits                           |
| `economy.wage_lower`               | int   | 25      | Server | Wage per interval for lower-tier jobs (assistant, visitor) |
| `economy.wage_crew`                | int   | 50      | Server | Wage per interval for standard crew jobs                   |
| `economy.wage_command`             | int   | 100     | Server | Wage per interval for command-tier jobs                    |
| `economy.vend_cargo_markup`        | float | 1.5     | Server | Multiplier on cargo value for vending price                |
| `economy.vend_material_markup`     | float | 0.5     | Server | Multiplier on recipe material cost for vending price       |
| `economy.vend_min_price`           | int   | 5       | Server | Absolute minimum vending price (last-resort fallback)      |

## Traits

<!-- TODO: game.max_trait_points (PR #376) not yet merged into release -->

| CVar                    | Type | Default | Scope      | Description                                            |
| ----------------------- | ---- | ------- | ---------- | ------------------------------------------------------ |
| `game.max_trait_points` | int  | 10      | Replicated | Global point budget shared across all trait categories |

## UI

| CVar             | Type   | Default  | Scope  | Description                 |
| ---------------- | ------ | -------- | ------ | --------------------------- |
| `ui.font_family` | string | NotoSans | Client | UI font family name         |
| `ui.font_size`   | int    | 12       | Client | UI base font size in points |
