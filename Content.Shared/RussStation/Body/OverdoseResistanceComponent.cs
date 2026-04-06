using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Body;

/// <summary>
/// Applied to a body entity when a cybernetic liver with overdose resistance is installed.
/// Increases the effective minimum threshold for ReagentCondition checks during metabolism,
/// raising the amount of reagent needed to trigger overdose effects.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class OverdoseResistanceComponent : Component
{
    /// <summary>
    /// Multiplier applied to ReagentCondition minimum thresholds.
    /// Values above 1.0 raise overdose thresholds (e.g., 1.5 = 50% more tolerance).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float ThresholdMultiplier = 1.5f;
}
