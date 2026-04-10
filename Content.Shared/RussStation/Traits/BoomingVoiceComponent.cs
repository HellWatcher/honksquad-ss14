using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Traits;

/// <summary>
/// Increases the entity's voice chat range by a multiplier.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BoomingVoiceComponent : Component
{
    [DataField]
    public float RangeMultiplier = 1.5f;
}
