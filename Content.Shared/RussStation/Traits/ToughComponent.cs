using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Traits;

/// <summary>
/// Raises wound spike thresholds, making the entity harder to wound.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ToughComponent : Component
{
    /// <summary>
    /// Multiplier applied to wound thresholds. Higher = wounds trigger less easily.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ThresholdMultiplier = 1.25f;
}
