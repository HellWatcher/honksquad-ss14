using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Traits;

/// <summary>
/// Slowly drains blood over time, eventually killing the entity
/// without medical intervention. Disables natural blood regeneration
/// and applies a periodic blood loss.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BloodDeficiencyComponent : Component
{
    /// <summary>
    /// Amount of blood removed per tick (every 3 seconds).
    /// At 0.2 with 300 blood, takes ~10 minutes to reach bloodloss threshold
    /// and ~75 minutes to fully drain.
    /// </summary>
    [DataField]
    public FixedPoint2 BloodLossPerTick = 0.2f;

    /// <summary>
    /// Time accumulator for periodic drain.
    /// </summary>
    [ViewVariables]
    public float Accumulator;
}
