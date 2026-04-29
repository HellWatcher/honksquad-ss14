using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.RussStation.Skillchips;

/// <summary>
/// Grants the mob a brief bullet-deflect window when they perform the trigger emote.
/// Added by the BULLET_DODGER skillchip.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BulletDodgeComponent : Component
{
    /// <summary>
    /// Emote that activates the dodge window.
    /// </summary>
    [DataField]
    public string ActivateEmoteId = "Salute";

    /// <summary>
    /// How long the dodge window stays active after the trigger emote.
    /// </summary>
    [DataField]
    public TimeSpan DodgeWindow = TimeSpan.FromSeconds(2.0);

    /// <summary>
    /// Stamina drained on each successful deflect.
    /// </summary>
    [DataField]
    public float StaminaCost = 30f;

    /// <summary>
    /// Server-only: when the active dodge window expires.
    /// </summary>
    [DataField]
    public TimeSpan? ActiveUntil;
}
