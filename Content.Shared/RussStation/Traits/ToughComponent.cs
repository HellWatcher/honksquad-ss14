using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Traits;

/// <summary>
/// Reduces all incoming damage by a configurable multiplier.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ToughComponent : Component
{
    [DataField]
    public FixedPoint2 DamageMultiplier = 0.85f;
}
