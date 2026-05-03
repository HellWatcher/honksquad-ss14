// HONK - Issue #679 step 4: bring stomach throughput in line with SS13.
// Two new knobs on the per-stage MetabolismSolutionEntry, both default 0 so the per-stage
// behaviour is opt-in and other organs (heart, liver, lung, kidney) stay on the upstream
// path until they explicitly need either knob:
//
//   * MinTransferPerTick mirrors SS13 STOMACH_METABOLISM_CONSTANT. Floors the per-reagent
//     transfer so a low MetabolismRate can still cross the heart's clearance rate.
//   * VolumeScaledTransfer mirrors SS13 metabolism_efficiency = 0.05. Adds a fraction of
//     the reagent's current volume in the source solution to the per-tick transfer, so a
//     fuller stomach delivers faster.
//
// Composed: per-tick transfer = max(rate, MinTransferPerTick) + VolumeScaledTransfer * quantity,
// clamped to quantity available.

using Content.Shared.FixedPoint;
using Content.Shared.RussStation.Metabolism;

namespace Content.Shared.Metabolism;

public sealed partial class MetabolismSolutionEntry
{
    /// <summary>
    /// Per-tick floor for reagent transfer at this stage. The actual move per tick is at
    /// least this many units of each reagent that has any quantity, clamped to quantity.
    /// 0 = disabled (upstream rate alone). Mirrors SS13 STOMACH_METABOLISM_CONSTANT.
    /// </summary>
    [DataField]
    public FixedPoint2 MinTransferPerTick = MetabolismConstants.DefaultMinTransferPerTick;

    /// <summary>
    /// Fraction of a reagent's current quantity in the source solution that adds to the
    /// per-tick transfer at this stage. Set to 0.05 on the stomach's Digestion entry so
    /// a fuller stomach pushes faster, matching SS13's <c>metabolism_efficiency</c>.
    /// 0 = disabled.
    /// </summary>
    [DataField]
    public float VolumeScaledTransfer = MetabolismConstants.DefaultVolumeScaledTransfer;
}
