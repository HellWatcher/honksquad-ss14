using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Traits;

[RegisterComponent, NetworkedComponent]
public sealed partial class SteadyHandComponent : Component
{
    [DataField]
    public float SpreadMultiplier = 0.5f;
}
