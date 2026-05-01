using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Body;

/// <summary>
/// Marker component for advanced cybernetic liver. The multiplier is read on the server during
/// metabolism; the component is networked so the engine's lifecycle hooks (EnsureComp/Dirty
/// during entity init) don't trip on a non-networked add. The corresponding effect on the host
/// body lives in <see cref="OverdoseResistanceComponent"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CyberneticLiverComponent : Component
{
    /// <summary>
    /// Multiplier applied to ReagentCondition minimum thresholds during metabolism.
    /// Values above 1.0 raise the overdose threshold (e.g., 1.5 = 50% higher tolerance).
    /// </summary>
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public float OverdoseThresholdMultiplier = CyberneticOrganConstants.DefaultOverdoseThresholdMultiplier;
}
