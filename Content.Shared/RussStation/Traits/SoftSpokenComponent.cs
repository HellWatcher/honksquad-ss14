using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Traits;

/// <summary>
/// Reduces the entity's voice range, making them harder to hear from a distance.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SoftSpokenComponent : Component
{
    /// <summary>
    /// Multiplier applied to voice range. 0.5 = half normal range.
    /// </summary>
    [DataField]
    public float RangeMultiplier = 0.5f;
}
