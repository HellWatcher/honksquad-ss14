using Content.Shared.FixedPoint;

namespace Content.Shared.RussStation.Metabolism;

/// <summary>
/// Constants for the metabolic chain refactor (#679). SS13 reference values live here so the
/// component / system code is just a name lookup, not a magic-number scatter.
/// </summary>
public static class MetabolismConstants
{
    /// <summary>
    /// Default per-stage transfer floor. 0 means no floor; the stomach overrides to 0.25u/tick
    /// to mirror SS13 STOMACH_METABOLISM_CONSTANT.
    /// </summary>
    public static readonly FixedPoint2 DefaultMinTransferPerTick = FixedPoint2.Zero;

    /// <summary>
    /// Default per-stage volume-scaled transfer fraction. 0 means flat rate; the stomach
    /// overrides to 0.05 (5% of the reagent's current quantity adds to the per-tick transfer)
    /// to mirror SS13 metabolism_efficiency.
    /// </summary>
    public const float DefaultVolumeScaledTransfer = 0f;
}
