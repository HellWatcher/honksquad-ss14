using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Body;

/// <summary>
/// Marker for advanced cybernetic eyes. While installed in a body, disables the lighting
/// requirement (DrawLight = false, same as standard) AND cancels flash attempts.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CyberneticEyesComponent : Component
{
    /// <summary>
    /// The original DrawLight value on the body at organ insertion, restored on removal.
    /// </summary>
    [ViewVariables]
    public bool OriginalDrawLight = true;
}
