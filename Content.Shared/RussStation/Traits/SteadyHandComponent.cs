using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Traits;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SteadyHandComponent : Component
{
    [DataField, AutoNetworkedField]
    public float SpreadMultiplier = TraitsConstants.SteadyHand.SpreadMultiplier;
}
