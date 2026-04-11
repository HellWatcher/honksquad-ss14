using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Traits;

/// <summary>
/// Scales the distance and speed of thrown items by a multiplier so
/// the character literally can't throw as far or as hard.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class WeakArmComponent : Component
{
    [DataField]
    public float ThrowMultiplier = 0.5f;
}
