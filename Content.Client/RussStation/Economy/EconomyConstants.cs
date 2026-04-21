namespace Content.Client.RussStation.Economy;

/// <summary>
/// Client-only numeric constants for the Economy system. UI layout values for
/// the balance cartridge fragment and related widgets.
/// </summary>
public static class EconomyConstants
{
    /// <summary>
    /// Top padding above the mute-toggle row in the balance cartridge UI.
    /// </summary>
    public const int MuteRowTopMargin = 8;

    /// <summary>
    /// Top padding above the transaction-list header label.
    /// </summary>
    public const int TxHeaderTopMargin = 12;

    /// <summary>
    /// Bottom padding below the transaction-list header label.
    /// </summary>
    public const int TxHeaderBottomMargin = 4;

    /// <summary>
    /// Minimum width (in virtual pixels) of the signed-amount column in each
    /// transaction row so amounts line up cleanly.
    /// </summary>
    public const int TxAmountColumnMinWidth = 60;
}
