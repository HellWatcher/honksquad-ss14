namespace Content.Shared.RussStation.Skillchips;

public static class BulletDodgeConstants
{
    /// <summary>Emote IDs that open the dodge window. Matches the in-engine Flip and Spin emotes.</summary>
    public static readonly string[] ActivateEmoteIds = ["Flip", "Spin"];

    /// <summary>Stamina cost charged on each successful deflect.</summary>
    public const float StaminaCost = 30f;
}
