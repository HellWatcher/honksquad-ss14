namespace Content.Shared.RussStation.Skillchips.Consumers;

public static class CleanbotWhispererConstants
{
    /// <summary>Tile radius scanned around each bot for chip-holders.</summary>
    public const float Range = 4f;

    /// <summary>Seconds between greeting attempts on each bot.</summary>
    public const int IntervalSeconds = 45;

    /// <summary>1-in-N chance to roll the rare easter-egg line instead of a common one.</summary>
    public const int RareOneIn = 20;
}
