using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Traits;

/// <summary>
/// Reduces the entity's examine range, requiring them to be much closer to examine things.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class TouchyComponent : Component
{
    /// <summary>
    /// The examine range when this component is active. Default 1.5 tiles (roughly touch distance).
    /// </summary>
    [DataField]
    public float ExamineRange = 1.5f;
}
