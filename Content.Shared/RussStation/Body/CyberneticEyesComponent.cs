using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Body;

/// <summary>
/// Marker for advanced cybernetic eyes. Cancels flash attempts while installed in a body.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CyberneticEyesComponent : Component;
