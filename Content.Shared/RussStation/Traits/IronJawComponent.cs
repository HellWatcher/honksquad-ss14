using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Traits;

/// <summary>
/// Increases the stamina crit threshold, making the entity harder to knock down.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class IronJawComponent : Component
{
    /// <summary>
    /// Multiplier applied to the stamina crit threshold.
    /// Higher = harder to knock down. Default 1.3 means 130% of normal threshold.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float CritThresholdModifier = 1.3f;
}
