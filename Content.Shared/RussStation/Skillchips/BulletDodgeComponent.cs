namespace Content.Shared.RussStation.Skillchips;

/// <summary>
/// Grants the mob a brief bullet-deflect window when they perform the trigger emote.
/// The window stays open for the duration of the triggering emote's animation, so
/// the visual matches the mechanical effect. Added by the BULLET_DODGER skillchip.
/// Server-only logic; not networked.
/// </summary>
[RegisterComponent]
public sealed partial class BulletDodgeComponent : Component
{
    /// <summary>
    /// Emotes that activate the dodge window. Any of these opens the window.
    /// </summary>
    [DataField]
    public HashSet<string> ActivateEmoteIds = new(BulletDodgeConstants.ActivateEmoteIds);

    /// <summary>
    /// Stamina drained on each successful deflect.
    /// </summary>
    [DataField]
    public float StaminaCost = BulletDodgeConstants.StaminaCost;

    /// <summary>
    /// Server-only: when the active dodge window expires.
    /// </summary>
    [DataField]
    public TimeSpan? ActiveUntil;
}
