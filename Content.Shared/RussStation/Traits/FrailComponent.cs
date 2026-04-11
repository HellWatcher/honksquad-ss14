using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Traits;

/// <summary>
/// Lowers wound spike thresholds, making the entity easier to wound.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FrailComponent : Component
{
    /// <summary>
    /// Multiplier applied to wound thresholds. Lower = wounds trigger more easily.
    /// </summary>
    [DataField]
    public float ThresholdMultiplier = 0.8f;
}
