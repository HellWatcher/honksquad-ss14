using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Traits;

[RegisterComponent, NetworkedComponent]
public sealed partial class ScarredEyeComponent : Component
{
    [DataField]
    public int Blindness = 2;
}
