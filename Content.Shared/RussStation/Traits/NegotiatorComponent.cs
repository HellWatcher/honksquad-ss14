using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Traits;

[RegisterComponent, NetworkedComponent]
public sealed partial class NegotiatorComponent : Component
{
    [DataField]
    public float WageMultiplier = 1.5f;
}
