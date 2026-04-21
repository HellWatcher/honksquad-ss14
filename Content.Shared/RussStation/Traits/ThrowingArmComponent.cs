using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Traits;

/// <summary>
/// Scales the distance and speed of thrown items by a multiplier so
/// the character throws further and faster.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ThrowingArmComponent : Component
{
    [DataField, AutoNetworkedField]
    public float ThrowMultiplier = TraitsConstants.ThrowingArm.ThrowMultiplier;
}
