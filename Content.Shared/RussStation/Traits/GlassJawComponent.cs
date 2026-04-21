using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Traits;

/// <summary>
/// Lowers the stamina crit threshold, making the entity easier to knock down
/// from stamina damage (batons, disablers, etc.).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GlassJawComponent : Component
{
    /// <summary>
    /// Multiplier applied to the stamina crit threshold via RefreshStaminaCritThresholdEvent.
    /// Lower = knocked down by less stamina damage. Default 0.7 means 70% of normal threshold.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float CritThresholdModifier = 0.7f;
}
