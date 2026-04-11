using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Traits;

/// <summary>
/// Scales the distance and speed of thrown items by a multiplier so
/// the character throws further and faster.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ThrowingArmComponent : Component
{
    [DataField]
    public float ThrowMultiplier = 1.5f;
}
