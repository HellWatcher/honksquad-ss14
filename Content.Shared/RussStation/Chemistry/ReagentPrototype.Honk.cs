// HONK - Issue #679 step 5: per-reagent tolerance for the kidney filter. Mirrors SS13's
// `liver_tolerance`: reagents below this floor in the bloodstream still drain each tick
// but skip effects, so micro-stacking ten 1u poisons can no longer cheese OD. Replaces
// the per-organ MaxReagentsProcessable cap (#679 step 3) as the anti-stacking mechanism.
//
// Default 0u means "every dose ticks normally"; the system applies a 3u default for
// reagents whose group is "Toxins" so the toxin class gets the SS13 floor automatically
// without touching the 30 toxin reagent prototypes.

using Content.Shared.FixedPoint;

namespace Content.Shared.Chemistry.Reagent;

public sealed partial class ReagentPrototype
{
    /// <summary>
    /// Minimum quantity in the active metabolism solution before the reagent's effects fire.
    /// Below this floor the reagent is still consumed each tick (so it isn't a free heal/buff),
    /// it just doesn't apply effects. Override to a positive value on a non-toxin proto when a
    /// reagent should resist micro-stacking. The toxin group gets a 3u baseline applied by the
    /// metabolism system; setting this field overrides that baseline.
    /// </summary>
    [DataField]
    public FixedPoint2 MinEffectiveDose = FixedPoint2.Zero;
}
