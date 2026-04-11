using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Traits;

/// <summary>
/// Reduces the duration of drunkenness by a configurable multiplier.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class AlcoholToleranceComponent : Component
{
    [DataField]
    public float BoozeStrengthMultiplier = 0.5f;
}
