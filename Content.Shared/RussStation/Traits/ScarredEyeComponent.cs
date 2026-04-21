using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Traits;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ScarredEyeComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Blindness = 2;
}
