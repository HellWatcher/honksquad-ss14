using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Traits;

/// <summary>
/// Increases the entity's voice chat range by a multiplier.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BoomingVoiceComponent : Component
{
    [DataField, AutoNetworkedField]
    public float RangeMultiplier = TraitsConstants.BoomingVoice.RangeMultiplier;
}
