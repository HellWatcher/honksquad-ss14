using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Traits;

/// <summary>
/// Increases the entity's examine range beyond the default.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class EagleEyeComponent : Component
{
    [DataField]
    public float ExamineRange = 24f;
}
