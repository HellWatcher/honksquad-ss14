namespace Content.Shared.RussStation.Bluespace;

/// <summary>
/// Tunable constants for the Bluespace crystal mechanics introduced by issue #302.
/// SS13 reference values live here so the component code is just a name lookup, not a
/// scatter of magic numbers.
/// </summary>
public static class BluespaceConstants
{
    /// <summary>
    /// Default blink range (in tiles) for a bluespace crystal. Mirrors SS13's
    /// <c>blink_range = 8</c> on <c>/obj/item/stack/ore/bluespace_crystal</c>.
    /// </summary>
    public const float DefaultBlinkRange = 8f;

    /// <summary>
    /// Default blink range (in tiles) when a metabolised reagent forces a
    /// teleport. Shorter than the active crystal blink so drinking dust feels
    /// weaker than a deliberate crush. Mirrors SS13's reagent default.
    /// </summary>
    public const float DefaultReagentBlinkRange = 5f;
}
