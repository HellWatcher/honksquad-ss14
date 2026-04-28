// HONK - Issue #491 Bug 1 (oral OD gap). Adds a per-stage minimum transfer rate so the
// stomach can guarantee at least N units of each reagent moves into the bloodstream every
// tick, regardless of the reagent's declared Digestion-stage MetabolismRate. Without a
// floor, oral medication asymptotes below Bloodstream-stage OD thresholds because
// (Digestion <= Bloodstream-clearance) per tick.
//
// Default 0 leaves upstream-tuned organs (heart, liver, lung) untouched. Stomach opts in
// via the Digestion entry default in the upstream component initializer (HONK-block).

using Content.Shared.FixedPoint;

namespace Content.Shared.Metabolism;

public sealed partial class MetabolismSolutionEntry
{
    /// <summary>
    /// Floor applied to per-tick reagent transfer for any reagent passing through this stage.
    /// When non-zero, every reagent that has any quantity in the source solution moves at least
    /// this much per tick (clamped to actual quantity). Set on the stomach's Digestion stage so
    /// oral medication can reach Bloodstream-stage OD/UD thresholds the way injections already do.
    /// 0 = disabled (upstream behaviour).
    /// </summary>
    [DataField]
    public FixedPoint2 MinTransferPerTick = FixedPoint2.Zero;
}
