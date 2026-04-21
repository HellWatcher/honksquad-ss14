using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Traits;

/// <summary>
/// Reduces footstep volume for quieter movement.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LightStepComponent : Component
{
    [DataField, AutoNetworkedField]
    public float VolumeModifier = TraitsConstants.LightStep.VolumeModifier;
}
