using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Body;

/// <summary>
/// Marker for standard cybernetic eyes. While installed in a body, disables
/// the lighting requirement (DrawLight = false), granting night vision.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CyberneticEyesStandardComponent : Component
{
    /// <summary>
    /// The original DrawLight value on the body at organ insertion, restored on removal.
    /// </summary>
    [ViewVariables]
    public bool OriginalDrawLight = true;
}
