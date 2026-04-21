using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Traits;

/// <summary>
/// Grants faster table climbing and immunity to glass table damage/stun.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FreerunningComponent : Component
{
    [DataField, AutoNetworkedField]
    public float ClimbDelayMultiplier = 0.5f;
}
