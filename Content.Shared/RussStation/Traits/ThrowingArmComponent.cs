using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Traits;

/// <summary>
/// Multiplies the entity's throw speed.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ThrowingArmComponent : Component
{
    [DataField]
    public float ThrowSpeedMultiplier = 1.5f;
}
