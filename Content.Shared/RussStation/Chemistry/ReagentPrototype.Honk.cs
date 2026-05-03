// HONK - Issue #679 step 5: per-reagent tolerance for the kidney filter. Mirrors SS13's
// `liver_tolerance`: reagents below this floor in the bloodstream still drain each tick
// but skip effects, so micro-stacking ten 1u poisons can no longer cheese OD. Replaces
// the per-organ MaxReagentsProcessable cap (#679 step 3) as the anti-stacking mechanism.
//
// Sentinel < 0 means "unset, use the system default" (currently 3u for everything).
// Set to 0 explicitly to opt a reagent out of the floor (e.g. medicines that need to
// fire at trace). Set to a positive value to override the floor up or down.

using Content.Shared.FixedPoint;

namespace Content.Shared.Chemistry.Reagent;

public sealed partial class ReagentPrototype
{
    /// <summary>
    /// Minimum quantity in the active metabolism solution before the reagent's effects fire.
    /// Below this floor the reagent is still consumed each tick (so it isn't a free heal/buff),
    /// it just doesn't apply effects. Negative means unset, the system applies the default
    /// floor; explicit 0 means no floor, fire at any dose; positive overrides the default.
    /// </summary>
    [DataField]
    public FixedPoint2 MinEffectiveDose = FixedPoint2.New(-1);
}
