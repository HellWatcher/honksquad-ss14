using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Traits;

/// <summary>
/// Reduces the duration of drunkenness by a configurable multiplier.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AlcoholToleranceComponent : Component
{
    [DataField, AutoNetworkedField]
    public float BoozeStrengthMultiplier = TraitsConstants.AlcoholTolerance.BoozeStrengthMultiplier;
}
