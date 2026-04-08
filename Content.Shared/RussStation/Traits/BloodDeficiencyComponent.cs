using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Traits;

/// <summary>
/// Reduces blood regeneration rate for entities with this component
/// by modifying BloodRefreshAmount on MapInit.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BloodDeficiencyComponent : Component
{
    /// <summary>
    /// Multiplier applied to BloodRefreshAmount.
    /// At 0.0 blood won't regenerate at all; at 0.5 it regenerates at half speed.
    /// </summary>
    [DataField]
    public float BloodRefreshMultiplier = 0.5f;
}
