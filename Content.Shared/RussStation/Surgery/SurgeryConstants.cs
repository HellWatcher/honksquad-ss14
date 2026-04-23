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

    /// <summary>
    /// Overlay entity spawned when a drape item carries no <see cref="Content.Shared.RussStation.Surgery.Components.SurgeryDrapeOverlayComponent"/>.
    /// Upstream bedsheets fall back to this; fork-side drape items override it on their prototype.
    /// </summary>
    public const string DefaultDrapeOverlayPrototype = "SurgeryDrapeOverlayBedsheet";

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

    // --- Step preset defaults ---

    /// <summary>Slash damage applied by a standard incision.</summary>
    public const float IncisionSlashDamage = 15f;

    /// <summary>Blunt damage applied by a standard retract step.</summary>
    public const float RetractBluntDamage = 6f;

    /// <summary>Slash damage applied by a saw step opening a deeper cavity.</summary>
    public const float SawSlashDamage = 9f;

    /// <summary>Flat healing budget baseline for tend-wound steps.</summary>
    public const float TendHealingFlat = 5f;

    /// <summary>Fraction of current eligible damage added to the tend healing budget.</summary>
    public const float TendHealingMultiplier = 0.07f;

    /// <summary>Mild bleed reduction a tend step applies on each repeat.</summary>
    public const float TendBleedReduction = -1f;

    /// <summary>Base duration (seconds) of an incision step before modifiers.</summary>
    public const float IncisionDuration = DefaultSlicingDuration;

    /// <summary>Base duration (seconds) of a retract step before modifiers.</summary>
    public const float RetractDuration = DefaultRetractingDuration;

    /// <summary>Base duration (seconds) of a clamp step before modifiers.</summary>
    public const float ClampDuration = DefaultClampingDuration;

    /// <summary>Base duration (seconds) of a saw step before modifiers.</summary>
    public const float SawDuration = DefaultSawingDuration;

    /// <summary>Base duration (seconds) of an organ-removal clamp before modifiers.</summary>
    public const float RemoveOrganDuration = DefaultClampingDuration;

    /// <summary>Duration of one tend-wound repeat in seconds.</summary>
    public const float TendStepDuration = 2.5f;

    /// <summary>Fallback duration used when a custom step declares no preset, no explicit duration,
    /// and no matching quality default. Picked low so a mis-authored step is still playable.</summary>
    public const float FallbackStepDuration = 2.0f;

    /// <summary>Container ID on the patient that holds the drape entity during surgery.</summary>
    public const string SurgeryDrapeContainerId = "surgery_drape";

    /// <summary>Duration of a burn-wound cautery / fracture bone-setting step in seconds.</summary>
    public const float WoundRepairDuration = 6f;

    /// <summary>
    /// Baseline bleed rate used as the unit when computing surgery-related bleed deltas. Natural
    /// bleed decay is 0.33 every 3 seconds (~0.11/s), so a value of 1.5 clots in roughly 14 seconds.
    /// </summary>
    public const float InterruptBleedBaseRate = 1.5f;

    /// <summary>
    /// Bleed amount added by an incision step and restored by an interrupt that unclamps a half-open
    /// patient. Three times the base so an un-clamped incision is a real time pressure (~40s to clot).
    /// </summary>
    public const float IncisionBleedAmount = InterruptBleedBaseRate * 3f;
}
