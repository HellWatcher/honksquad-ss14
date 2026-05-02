using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Body;

/// <summary>
/// Added to a body that has no Heart-category organ installed.
/// Drives saturation drain in HeartlessSystem -- not removable by defibrillation.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class NoHeartComponent : Component;
