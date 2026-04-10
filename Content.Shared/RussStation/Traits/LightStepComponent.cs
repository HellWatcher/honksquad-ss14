using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Traits;

/// <summary>
/// Reduces footstep volume for quieter movement.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class LightStepComponent : Component
{
    [DataField]
    public float VolumeModifier = -10f;
}
