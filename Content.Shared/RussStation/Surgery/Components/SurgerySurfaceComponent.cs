using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Surgery.Components;

/// <summary>
/// Marks furniture as a surgery surface that provides a speed bonus when a patient is buckled to it.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SurgerySurfaceComponent : Component
{
    /// <summary>
    /// Multiplier applied to surgery step durations. Below 1.0 speeds things up, above 1.0 slows them down.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float SpeedModifier = 1f;
}
