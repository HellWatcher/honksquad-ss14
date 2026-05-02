using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Body;

/// <summary>
/// Marker component for advanced cybernetic ears.
/// While the organ is implanted, cancels deafness attempts (deafness resistance).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CyberneticEarsComponent : Component
{
}
