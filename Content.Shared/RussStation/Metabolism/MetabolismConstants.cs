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

    /// <summary>
    /// Default per-stage toxin scrub rate. 0 means the stage does no toxin scrubbing; the
    /// liver's Detoxification stage overrides to 1u/tick so toxin-class reagents in the
    /// bloodstream get pulled out at a steady rate. Mirrors SS13's liver toxin filter.
    /// </summary>
    public static readonly FixedPoint2 DefaultToxinScrubRate = FixedPoint2.Zero;

    /// <summary>
    /// Liver's per-tick toxin scrub on the bloodstream. Tunable knob; start small enough
    /// that a healthy liver does not trivialize toxin damage but big enough to make liver
    /// failure feel noticeable.
    /// </summary>
    public static readonly FixedPoint2 LiverToxinScrubRate = FixedPoint2.New(1);
}
