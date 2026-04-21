namespace Content.Client.RussStation.Economy;

/// <summary>
/// Client-only numeric constants for the Economy system. UI layout values for
/// the balance cartridge fragment and related widgets.
/// </summary>
public static class EconomyConstants
{
    public const int MuteRowLeftMargin = 0;

    public const int MuteRowTopMargin = 8;

    public const int MuteRowRightMargin = 0;

    public const int MuteRowBottomMargin = 0;

    public const int TxHeaderLeftMargin = 0;

    public const int TxHeaderTopMargin = 12;

    public const int TxHeaderRightMargin = 0;

    public const int TxHeaderBottomMargin = 4;

    /// <summary>
    /// Minimum width (in virtual pixels) of the signed-amount column in each
    /// transaction row so amounts line up cleanly.
    /// </summary>
    public const int TxAmountColumnMinWidth = 60;

    /// <summary>
    /// Index of the primary content <c>BoxContainer</c> inside the balance
    /// cartridge fragment root. The first child (index 0) is the background
    /// <c>PanelContainer</c>; the second (index 1) holds the cartridge UI.
    /// </summary>
    public const int RootContentChildIndex = 1;
}
