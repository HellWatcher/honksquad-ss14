namespace Content.Shared.RussStation.Economy;

/// <summary>
/// Shared numeric constants for the Economy system. Values that are user-tunable
/// belong in <see cref="EconomyCCVars"/> instead; this file is for internal
/// non-configurable values whose meaning should be named rather than inlined.
/// </summary>
public static class EconomyConstants
{
    /// <summary>
    /// Number of trailing characters of an account number shown as a short suffix
    /// in the balance cartridge UI (e.g. "...ABCD").
    /// </summary>
    public const int AccountSuffixLength = 4;

    /// <summary>
    /// Maximum absolute jitter, in seconds, added to a player's first payroll
    /// timer so that paychecks don't all land on the same tick.
    /// </summary>
    public const float PayrollJitterSeconds = 60f;

    /// <summary>
    /// Vending prices are rounded up to the nearest multiple of this value.
    /// </summary>
    public const int VendingPriceRoundingStep = 5;
}
