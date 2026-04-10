using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Traits;

/// <summary>
/// Increases all incoming damage by a configurable multiplier.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FrailComponent : Component
{
    [DataField]
    public FixedPoint2 DamageMultiplier = 1.25f;
}
