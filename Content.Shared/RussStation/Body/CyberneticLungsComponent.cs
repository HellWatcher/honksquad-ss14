using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Body;

/// <summary>
/// Marker component for advanced cybernetic lungs that filter toxic gases.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CyberneticLungsComponent : Component
{
    /// <summary>
    /// Fraction of toxic gas moles filtered out before reaching the lungs.
    /// 0.5 = 50% reduction.
    /// </summary>
    [DataField]
    public float FilterFraction = 0.5f;
}
