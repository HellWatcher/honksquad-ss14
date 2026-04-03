using Robust.Shared.Configuration;

namespace Content.Shared.RussStation.Economy;

[CVarDefs]
public sealed class EconomyCCVars
{
    /// <summary>
    /// Default starting balance granted to players on spawn (in spesos).
    /// </summary>
    public static readonly CVarDef<int> DefaultStartingBalance =
        CVarDef.Create("economy.default_starting_balance", 100, CVar.SERVERONLY);
}
