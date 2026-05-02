using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Body;

/// <summary>
/// Marker for basic cybernetic ears. While installed in a body, applies
/// HearingImpairmentComponent to the host, muffling all audio.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CyberneticEarsBasicComponent : Component;
