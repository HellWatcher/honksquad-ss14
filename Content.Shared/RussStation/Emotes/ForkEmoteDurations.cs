namespace Content.Shared.RussStation.Emotes;

/// <summary>
/// Animation durations for the fork-added physical emotes. Single source of truth
/// for both the emote system that drives the visual and any consumer (e.g. the
/// BULLET_DODGER skillchip) that needs to align its window with the animation.
/// </summary>
public static class ForkEmoteDurations
{
    public static readonly TimeSpan Flip = TimeSpan.FromSeconds(0.5);
    public static readonly TimeSpan Spin = TimeSpan.FromSeconds(0.5);
}
