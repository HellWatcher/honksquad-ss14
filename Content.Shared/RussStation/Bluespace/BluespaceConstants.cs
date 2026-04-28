namespace Content.Shared.RussStation.Bluespace;

/// <summary>
/// Tunable constants for the Bluespace mechanics introduced by issue #302 and its
/// follow-ups. SS13 reference values live here so the component code is a name
/// lookup rather than a scatter of magic numbers.
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

    /// <summary>
    /// Default blink range (in tiles) for bluespace plants. SS13 scales by
    /// <c>potency / 10</c> on <c>/datum/plant_gene/trait/teleport</c>; this is
    /// the flat per-fruit equivalent. 5 lands between the raw ore (8) and a
    /// typical SS13 mid-potency plant.
    /// </summary>
    public const float PlantBlinkRange = 5f;

    /// <summary>
    /// Default fumble probability for the bluespace tomato hold-backfire. SS13
    /// rolls 50% per attempted use on <c>/datum/plant_gene/trait/backfire/bluespace</c>.
    /// </summary>
    public const float PlantBackfireProbability = 0.5f;
}
