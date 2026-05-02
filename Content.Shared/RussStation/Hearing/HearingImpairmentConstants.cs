namespace Content.Shared.RussStation.Hearing;

/// <summary>
/// Tunable defaults for hearing-impairment audio occlusion.
/// </summary>
public static class HearingImpairmentConstants
{
    /// <summary>Default extra audio occlusion applied while
    /// <see cref="HearingImpairmentComponent"/> is on a mob. Lower than the full deaf
    /// value of 8 -- audible but noticeably muffled.</summary>
    public const float DefaultOcclusionBonus = 3.5f;
}
