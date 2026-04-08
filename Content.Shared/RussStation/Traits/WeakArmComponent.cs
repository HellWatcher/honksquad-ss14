using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Traits;

/// <summary>
/// Reduces throw speed by a configurable multiplier.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class WeakArmComponent : Component
{
    [DataField]
    public float ThrowSpeedMultiplier = 0.5f;
}
