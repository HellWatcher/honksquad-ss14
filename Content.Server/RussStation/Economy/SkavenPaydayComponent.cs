// HONK - Issue #481 P4: Skaven payday modifier. Skaven get a fraction of standard wages
// per SS13 lore (skaven_payday_multiplier = 0.25). The actual multiplier lives in
// EconomyConstants and is read by SkavenPaydaySystem on every GetWageEvent the entity
// raises.

namespace Content.Server.RussStation.Economy;

[RegisterComponent]
public sealed partial class SkavenPaydayComponent : Component;
