using Content.Shared.Alert;
using Content.Shared.Buckle.Components;
using Content.Shared.Examine;
using Content.Shared.RussStation.Surgery.Components;
using Content.Shared.Tools;
using Content.Shared.Tools.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared.RussStation.Surgery.Systems;

public abstract partial class SharedSurgerySystem : EntitySystem
{
    protected static readonly ProtoId<AlertPrototype> SurgeryDrapedAlert = "SurgeryDraped";

    private static readonly ProtoId<ToolQualityPrototype> CauterizingQuality = "Cauterizing";

    [Dependency] protected readonly AlertsSystem _alerts = default!;
    [Dependency] protected readonly SharedToolSystem _tool = default!;
    [Dependency] protected readonly IPrototypeManager ProtoManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SurgeryDrapedComponent, ComponentShutdown>(OnDrapedShutdown);
        SubscribeLocalEvent<ActiveSurgeryComponent, ExaminedEvent>(OnActiveSurgeryExamined);
        SubscribeLocalEvent<SurgeryDrapedComponent, ExaminedEvent>(OnDrapedExamined);
    }

    private void OnActiveSurgeryExamined(Entity<ActiveSurgeryComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.ProcedureId != null &&
            ProtoManager.TryIndex<SurgeryProcedurePrototype>(ent.Comp.ProcedureId.Value, out var proto))
        {
            args.PushMarkup(Loc.GetString("surgery-examine-active",
                ("target", ent.Owner), ("procedure", Loc.GetString(proto.Name))));
        }
    }

    private void OnDrapedExamined(Entity<SurgeryDrapedComponent> ent, ref ExaminedEvent args)
    {
        if (!HasComp<ActiveSurgeryComponent>(ent))
            args.PushMarkup(Loc.GetString("surgery-examine-draped", ("target", ent.Owner)));
    }

    private void OnDrapedShutdown(Entity<SurgeryDrapedComponent> ent, ref ComponentShutdown args)
    {
        _alerts.ClearAlert(ent.Owner, SurgeryDrapedAlert);

        // Remove the visible drape overlay (spawned server-side on startup).
        if (ent.Comp.OverlayEntity is { } overlay && Exists(overlay))
            QueueDel(overlay);

        // Drop bedsheet/drape when draping is removed
        if (ent.Comp.Bedsheet is not { } bedsheet || !Exists(bedsheet))
            return;

        var xformSys = EntityManager.System<SharedTransformSystem>();
        xformSys.DropNextTo(bedsheet, ent.Owner);
    }

    /// <summary>
    /// Checks if the given tool has the required quality for a surgery step.
    /// </summary>
    public bool ToolMatchesStep(EntityUid tool, SurgeryStep step)
    {
        return _tool.HasQuality(tool, step.GetQuality());
    }

    /// <summary>
    /// Gets the base duration for a step: explicit override, preset default, or fallback. Every
    /// in-use preset now carries a duration, so hitting the fallback means a procedure authored
    /// with <see cref="SurgeryStepPreset.None"/> and no explicit duration.
    /// </summary>
    public static float GetBaseStepDuration(SurgeryStep step)
    {
        return step.GetDuration() ?? SurgeryConstants.FallbackStepDuration;
    }

    /// <summary>
    /// Gets the effective DoAfter duration for a surgery step, incorporating surface, drape, and
    /// difficulty modifiers (not tool tier, that's server-side). Also raises a
    /// <see cref="SurgeryStepDurationModifierEvent"/> so subscribers can stack their own per-step
    /// multipliers without touching this system.
    /// </summary>
    public TimeSpan GetStepDuration(SurgeryStep step, EntityUid patient, SurgeryDifficulty difficulty, EntityUid? surgeon = null)
    {
        var ev = new SurgeryStepDurationModifierEvent(step, patient, surgeon);
        RaiseLocalEvent(patient, ref ev);
        if (surgeon is { } surgeonUid)
            RaiseLocalEvent(surgeonUid, ref ev);

        return TimeSpan.FromSeconds(GetBaseStepDuration(step)
            * GetSurfaceSpeedModifier(patient)
            * GetDrapeSpeedModifier(patient)
            * GetDifficultyModifier(difficulty)
            * ev.Multiplier);
    }

    /// <summary>
    /// Gets the speed modifier from the surface the patient is buckled to, if any.
    /// </summary>
    public float GetSurfaceSpeedModifier(EntityUid patient)
    {
        if (!TryComp<BuckleComponent>(patient, out var buckle) || buckle.BuckledTo is not { } strap)
            return SurgeryConstants.NoSurgerySurfacePenalty;

        if (!TryComp<SurgerySurfaceComponent>(strap, out var surface))
            return SurgeryConstants.NoSurgerySurfacePenalty;

        return surface.SpeedModifier;
    }

    /// <summary>
    /// Gets the speed modifier from the drape material on the patient.
    /// Surgical drapes = 1.0, bedsheets = 1.5 (default).
    /// </summary>
    public float GetDrapeSpeedModifier(EntityUid patient)
    {
        if (!TryComp<SurgeryDrapedComponent>(patient, out var draped))
            return SurgeryConstants.NoDrapeSpeedModifier;

        return draped.DrapeSpeedModifier;
    }

    /// <summary>
    /// Maps a procedure difficulty tier to a duration multiplier.
    /// </summary>
    public static float GetDifficultyModifier(SurgeryDifficulty difficulty)
    {
        return difficulty switch
        {
            SurgeryDifficulty.Minor => SurgeryConstants.DifficultyMinorModifier,
            SurgeryDifficulty.Standard => SurgeryConstants.DifficultyStandardModifier,
            SurgeryDifficulty.Major => SurgeryConstants.DifficultyMajorModifier,
            SurgeryDifficulty.Critical => SurgeryConstants.DifficultyCriticalModifier,
            _ => SurgeryConstants.DifficultyStandardModifier,
        };
    }

    /// <summary>
    /// Checks if the tool has the Cauterizing quality for universal close.
    /// </summary>
    public bool IsCauteryTool(EntityUid tool)
    {
        return _tool.HasQuality(tool, CauterizingQuality);
    }
}
