using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Body;

/// <summary>
/// Applied to a body entity when a cybernetic liver with overdose resistance is installed.
/// Networked so EnsureComp/Dirty during entity init doesn't trip; the multiplier is read in
/// MetabolizerSystem.CanMetabolizeEffect (server-only) so we don't need to actively replicate
/// the field, but the component presence itself is part of the body's networked state.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OverdoseResistanceComponent : Component
{
    /// <summary>
    /// Multiplier applied to ReagentCondition minimum thresholds.
    /// Values above 1.0 raise overdose thresholds (e.g., 1.5 = 50% more tolerance).
    /// </summary>
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public float ThresholdMultiplier = CyberneticOrganConstants.DefaultOverdoseThresholdMultiplier;
}
