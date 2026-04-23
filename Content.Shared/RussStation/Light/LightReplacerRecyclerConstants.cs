namespace Content.Shared.RussStation.Light;

public static class LightReplacerRecyclerConstants
{
    /// <summary>
    /// Recycle points earned per broken bulb or glass shard consumed.
    /// </summary>
    public const int DefaultPointsPerRecycle = 1;

    /// <summary>
    /// Recycle points spent to print one new bulb.
    /// </summary>
    public const int DefaultPrintCost = 3;

    /// <summary>
    /// Bulb storage cap. Matches a full light box (BoxLightbulb / BoxLighttube fill with 12).
    /// </summary>
    public const int DefaultMaxStoredBulbs = 12;
}
