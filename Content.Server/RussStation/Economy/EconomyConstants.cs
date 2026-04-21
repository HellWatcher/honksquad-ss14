namespace Content.Server.RussStation.Economy;

/// <summary>
/// Server-only numeric constants for the Economy system. Values used only by
/// server-side subsystems (audio volume, account number generation, etc.).
/// </summary>
public static class EconomyConstants
{
    /// <summary>
    /// Volume (in dB) applied to the paycheck notification chime played on the
    /// holder's PDA when a wage is deposited.
    /// </summary>
    public const float PaycheckChimeVolume = -6f;

    /// <summary>
    /// Minimum numeric value for a generated account number. Guarantees that
    /// <see cref="AccountNumberHexFormat"/> always produces a full 8-hex-digit
    /// string with no leading-zero collisions.
    /// </summary>
    public const int AccountNumberMinValue = 0x10000000;

    /// <summary>
    /// Format specifier for generated account numbers (8-digit uppercase hex).
    /// </summary>
    public const string AccountNumberHexFormat = "X8";

    /// <summary>
    /// Default starting balance for manually-created bank accounts (as opposed
    /// to spawn-time accounts which use the <c>DefaultStartingBalance</c> CVar).
    /// Creating a new account forfeits any previous account's funds, so the
    /// new account starts empty as an intentional penalty.
    /// </summary>
    public const int NewAccountStartingBalance = 0;
}
