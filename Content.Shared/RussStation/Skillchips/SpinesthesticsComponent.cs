using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Skillchips;

/// <summary>
/// Grants the mob access to Spin and Flip emotes, but degrades over time.
/// Each use has an increasing chance to fire side effects (sparks, cellular damage, explosion).
/// Added by the Old F058UR7 skillchip.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SpinesthesticsComponent : Component
{
    /// <summary>
    /// How many uses remain before degradation side effects begin.
    /// Counts down with each Spin or Flip.
    /// </summary>
    [DataField]
    public int UsesRemaining = 14;

    /// <summary>
    /// Below this threshold, sparks fire and minor cellular damage occurs.
    /// </summary>
    [DataField]
    public int SparksThreshold = 10;

    /// <summary>
    /// Below this threshold, heavy cellular damage occurs.
    /// </summary>
    [DataField]
    public int SmokeThreshold = 5;

    /// <summary>
    /// Below this threshold, a small explosion damages the mob.
    /// </summary>
    [DataField]
    public int ExplosionThreshold = 2;
}
