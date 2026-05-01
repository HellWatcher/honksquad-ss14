using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Body;

/// <summary>
/// Marker component for advanced cybernetic lungs. Currently no extra effect beyond the
/// shared CyberneticOrgan EMP behavior; toxic-gas filtering is deferred to a follow-up PR.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CyberneticLungsComponent : Component;
