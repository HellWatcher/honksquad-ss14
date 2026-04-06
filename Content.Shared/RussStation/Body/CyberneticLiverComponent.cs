using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Body;

/// <summary>
/// Marker component for advanced cybernetic liver.
/// While the organ is implanted, increases overdose thresholds for all reagents,
/// allowing the body to tolerate higher concentrations before overdose effects trigger.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CyberneticLiverComponent : Component
{
    /// <summary>
    /// Multiplier applied to ReagentCondition minimum thresholds during metabolism.
    /// Values above 1.0 raise the overdose threshold (e.g., 1.5 = 50% higher tolerance).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float OverdoseThresholdMultiplier = 1.5f;
}
