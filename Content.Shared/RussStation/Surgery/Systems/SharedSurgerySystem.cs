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
    private static readonly ProtoId<ToolQualityPrototype> CauterizingQuality = "Cauterizing";

    /// <summary>
    /// Default step durations per tool quality, used when a step doesn't specify its own.
    /// </summary>
    private static readonly Dictionary<string, float> DefaultStepDurations = new()
    {
        { "Slicing", 2.0f },
        { "Retracting", 1.5f },
        { "Clamping", 2.0f },
        { "Sawing", 3.0f },
        { "Drilling", 2.0f },
        { "Cauterizing", 2.0f },
    };

    private const float FallbackStepDuration = 2.0f;

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
        _alerts.ClearAlert(ent.Owner, "SurgeryDraped");

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
        return _tool.HasQuality(tool, step.Quality);
    }

    /// <summary>
    /// Gets the base duration for a step: explicit override or centralized default.
    /// </summary>
    public static float GetBaseStepDuration(SurgeryStep step)
    {
        if (step.Duration.HasValue)
            return step.Duration.Value;

        return DefaultStepDurations.GetValueOrDefault(step.Quality, FallbackStepDuration);
    }

    /// <summary>
    /// Gets the effective DoAfter duration for a surgery step, incorporating
    /// surface, drape, and difficulty modifiers (not tool tier, that's server-side).
    /// </summary>
    public TimeSpan GetStepDuration(SurgeryStep step, EntityUid patient, SurgeryDifficulty difficulty)
    {
        return TimeSpan.FromSeconds(GetBaseStepDuration(step)
            * GetSurfaceSpeedModifier(patient)
            * GetDrapeSpeedModifier(patient)
            * GetDifficultyModifier(difficulty));
    }

    /// <summary>
    /// Gets the speed modifier from the surface the patient is buckled to, if any.
    /// </summary>
    public float GetSurfaceSpeedModifier(EntityUid patient)
    {
        if (!TryComp<BuckleComponent>(patient, out var buckle) || buckle.BuckledTo is not { } strap)
            return 2f;

        if (!TryComp<SurgerySurfaceComponent>(strap, out var surface))
            return 2f;

        return surface.SpeedModifier;
    }

    /// <summary>
    /// Gets the speed modifier from the drape material on the patient.
    /// Surgical drapes = 1.0, bedsheets = 1.5 (default).
    /// </summary>
    public float GetDrapeSpeedModifier(EntityUid patient)
    {
        if (!TryComp<SurgeryDrapedComponent>(patient, out var draped))
            return 1f;

        return draped.DrapeSpeedModifier;
    }

    /// <summary>
    /// Maps a procedure difficulty tier to a duration multiplier.
    /// </summary>
    public static float GetDifficultyModifier(SurgeryDifficulty difficulty)
    {
        return difficulty switch
        {
            SurgeryDifficulty.Minor => 0.8f,
            SurgeryDifficulty.Standard => 1.0f,
            SurgeryDifficulty.Major => 1.3f,
            SurgeryDifficulty.Critical => 1.5f,
            _ => 1.0f,
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
