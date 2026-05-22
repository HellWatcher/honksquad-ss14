namespace Content.Shared.RussStation.Skillchips.Consumers;

public static class SabrageConstants
{
    /// <summary>Multiplier on sqrt(weapon damage) feeding the success chance.</summary>
    public const float SuccessSqrtFactor = 20f;

    /// <summary>Flat offset on the no-chip term; negative so weak weapons don't get a free coin-flip.</summary>
    public const float BaseOffset = -33f;

    /// <summary>Multiplier applied when the user carries the <c>sabrage_proficiency</c> capability.</summary>
    public const float SkillchipMultiplier = 2f;

    /// <summary>Minimum weapon damage required to attempt sabrage at all. SS13 force gate is 5.</summary>
    public const float MinimumDamage = 5f;

    /// <summary>Wind-up swing duration in seconds.</summary>
    public const int SwingSeconds = 2;
}
