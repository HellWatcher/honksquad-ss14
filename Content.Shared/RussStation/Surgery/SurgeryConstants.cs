namespace Content.Shared.RussStation.Surgery;

public static class SurgeryConstants
{
    public const float CauteryCloseBaseDurationSeconds = 2f;

    public const float ToolTierExperimentalModifier = 0.7f;
    public const float ToolTierAdvancedModifier = 0.8f;
    public const float ToolTierStandardModifier = 1.0f;
    public const float ToolTierImprovisedModifier = 1.5f;

    public const int CauteryBurnDamage = 2;
    public const float CauteryBleedClearAmount = -100f;

    public const float DefaultDrapeSpeedModifier = 1.5f;

    public const float DefaultTrayFoldedFriction = 0.8f;
    public const float DefaultTrayUnfoldedFriction = 0.4f;

    public const float DefaultSlicingDuration = 2.0f;
    public const float DefaultRetractingDuration = 1.5f;
    public const float DefaultClampingDuration = 2.0f;
    public const float DefaultSawingDuration = 3.0f;
    public const float DefaultDrillingDuration = 2.0f;
    public const float DefaultBoneSettingDuration = 3.0f;
    public const float DefaultCauterizingDuration = 2.0f;

    public const float NoSurgerySurfacePenalty = 2f;

    public const float DifficultyMinorModifier = 0.8f;
    public const float DifficultyStandardModifier = 1.0f;
    public const float DifficultyMajorModifier = 1.3f;
    public const float DifficultyCriticalModifier = 1.5f;

    /// <summary>
    /// Default SurgerySurfaceComponent speed modifier: neutral baseline (no slowdown, no boost).
    /// </summary>
    public const float DefaultSurgerySurfaceSpeedModifier = 1f;

    /// <summary>
    /// Speed modifier returned when a patient has no drape component: neutral baseline.
    /// </summary>
    public const float NoDrapeSpeedModifier = 1f;

    /// <summary>
    /// Step index used when a new surgery begins: start at the first procedure step.
    /// </summary>
    public const int InitialProcedureStepIndex = 0;

    /// <summary>
    /// Epsilon threshold for treating a damage type as effectively zero when
    /// deciding whether a healing step has anything to do.
    /// </summary>
    public const float HealingDamageEpsilon = 0.01f;
}
