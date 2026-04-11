using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Traits;

/// <summary>
/// Reduces the entity's payroll wages by a multiplier.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class IndebtedComponent : Component
{
    [DataField]
    public float WageMultiplier = 0.5f;
}
