using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Traits;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NegotiatorComponent : Component
{
    [DataField, AutoNetworkedField]
    public float WageMultiplier = TraitsConstants.Negotiator.WageMultiplier;
}
