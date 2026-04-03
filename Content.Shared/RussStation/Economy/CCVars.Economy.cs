using Robust.Shared.Configuration;

namespace Content.Shared.RussStation.Economy;

[CVarDefs]
public sealed class EconomyCCVars
{
    /// <summary>
    /// Default starting balance granted to players on spawn (in spesos).
    /// </summary>
    public static readonly CVarDef<int> DefaultStartingBalance =
        CVarDef.Create("economy.default_starting_balance", 250, CVar.SERVERONLY);

    /// <summary>
    /// Payroll interval in seconds. Wages are deposited every this many seconds.
    /// </summary>
    public static readonly CVarDef<float> PayrollInterval =
        CVarDef.Create("economy.payroll_interval", 300f, CVar.SERVERONLY);

    /// <summary>
    /// Wage for lower-tier jobs (assistant, visitor) per payroll interval.
    /// </summary>
    public static readonly CVarDef<int> WageLower =
        CVarDef.Create("economy.wage_lower", 25, CVar.SERVERONLY);

    /// <summary>
    /// Wage for standard crew jobs per payroll interval.
    /// </summary>
    public static readonly CVarDef<int> WageCrew =
        CVarDef.Create("economy.wage_crew", 50, CVar.SERVERONLY);

    /// <summary>
    /// Wage for command-tier jobs per payroll interval.
    /// </summary>
    public static readonly CVarDef<int> WageCommand =
        CVarDef.Create("economy.wage_command", 100, CVar.SERVERONLY);

    /// <summary>
    /// Flat cost per vending machine purchase (in spesos). Set to 0 to disable payment.
    /// </summary>
    public static readonly CVarDef<int> VendPrice =
        CVarDef.Create("economy.vend_price", 5, CVar.SERVERONLY);
}
