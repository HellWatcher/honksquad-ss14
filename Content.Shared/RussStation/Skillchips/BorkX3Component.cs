using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Skillchips;

/// <summary>
/// Grants CQC bonuses when fighting inside a kitchen zone.
/// Added by the B0RK-X3 skillchip.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BorkX3Component : Component
{
    /// <summary>
    /// Extra Blunt damage added to each unarmed hit while in the kitchen.
    /// </summary>
    [DataField]
    public float BonusDamage = 8f;

    /// <summary>
    /// Probability (0-1) that a kitchen hit knocks the target down.
    /// </summary>
    [DataField]
    public float KnockdownChance = 0.25f;

    /// <summary>
    /// How long the knockdown lasts.
    /// </summary>
    [DataField]
    public TimeSpan KnockdownDuration = TimeSpan.FromSeconds(1.5);
}
