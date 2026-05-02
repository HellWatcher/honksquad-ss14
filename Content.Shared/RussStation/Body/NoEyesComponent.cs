using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Body;

/// <summary>
/// Added to a body that has no Eyes-category organ installed.
/// Drives blindness in <see cref="Content.Server.RussStation.Body.EyelessSystem"/>.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class NoEyesComponent : Component;
