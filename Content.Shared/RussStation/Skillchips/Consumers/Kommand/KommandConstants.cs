namespace Content.Shared.RussStation.Skillchips.Consumers.Kommand;

/// <summary>
/// Tunables for the Kommand chip's enhanced pointer. Lifted out of the
/// component's DataField initializer per HONK0016.
/// </summary>
public static class KommandConstants
{
    /// <summary>
    /// Sprite scale multiplier the client visualizer applies to the enhanced PointingArrow.
    /// SS13's "arrow_large" sprite is roughly 1.5x the regular arrow, this matches the
    /// visual difference without porting a dedicated sprite.
    /// </summary>
    public const float ArrowScale = 1.5f;
}
