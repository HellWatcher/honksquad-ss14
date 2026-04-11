using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Traits;

/// <summary>
/// Grants faster table climbing and immunity to glass table damage/stun.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FreerunningComponent : Component
{
    [DataField]
    public float ClimbDelayMultiplier = 0.5f;
}
