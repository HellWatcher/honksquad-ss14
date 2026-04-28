// HONK - Issue #491 Bug 1, sub-tolerance filter. Mirrors SS13's per-reagent
// `liver_tolerance` knob (russ-station/code/modules/reagents/chemistry/holder/mob_life.dm:38-45):
// reagents below an effective dose still drain through the body but never fire effects, so
// stacking ten 1u poisons can no longer cheese OD. Replaces the per-organ MaxReagentsProcessable
// throttle as the anti-stack mechanism.

using Content.Shared.FixedPoint;

namespace Content.Shared.Chemistry.Reagent;

public sealed partial class ReagentPrototype
{
    /// <summary>
    /// Minimum quantity in the active metabolism solution before the reagent's effects fire.
    /// Below this floor the reagent is still consumed each tick (so it isn't a free heal/buff),
    /// it just doesn't apply effects. Default 3u matches SS13's `liver_tolerance` baseline.
    /// Set to 0 on a reagent proto to opt out (always fire, even at trace volumes).
    /// </summary>
    [DataField]
    public FixedPoint2 MinEffectiveDose = FixedPoint2.New(3);
}
