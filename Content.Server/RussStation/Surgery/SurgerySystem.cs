using Content.Shared.Body;
using Content.Shared.Body.Systems;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.RussStation.Surgery;
using Content.Shared.RussStation.Surgery.Components;
using Content.Shared.RussStation.Surgery.Systems;
using Content.Shared.Standing;
using Content.Shared.Tag;
using Content.Shared.Tools;
using Content.Shared.Tools.Components;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.RussStation.Surgery;

public sealed partial class SurgerySystem : SharedSurgerySystem
{
    private static readonly ProtoId<ToolQualityPrototype> DrapingQuality = "Draping";

    private static readonly ProtoId<TagPrototype> TierStandardTag = "TierStandard";
    private static readonly ProtoId<TagPrototype> TierAdvancedTag = "TierAdvanced";
    private static readonly ProtoId<TagPrototype> TierExperimentalTag = "TierExperimental";

    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedBloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;

    private List<string> _cachedProcedureIds = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BodyComponent, AfterInteractUsingEvent>(OnAfterInteract);
        SubscribeLocalEvent<ActiveSurgeryComponent, SurgeryStepDoAfterEvent>(OnStepDoAfter);
        SubscribeLocalEvent<ActiveSurgeryComponent, SurgeryCauteryDoAfterEvent>(OnCauteryDoAfter);
        SubscribeLocalEvent<SurgeryDrapedComponent, ComponentStartup>(OnDrapedStartup);
        SubscribeLocalEvent<SurgeryDrapedComponent, RemoveSurgeryDrapeAlertEvent>(OnRemoveDrapeAlert);

        SubscribeNetworkEvent<SelectSurgeryProcedureEvent>(OnProcedureSelected);
        SubscribeNetworkEvent<SelectOrganEvent>(OnOrganSelected);

        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);

        InitializeOrgans();
        CacheProcedureIds();
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<SurgeryProcedurePrototype>())
            CacheProcedureIds();
    }

    private void CacheProcedureIds()
    {
        _cachedProcedureIds.Clear();
        foreach (var proto in ProtoManager.EnumeratePrototypes<SurgeryProcedurePrototype>())
        {
            _cachedProcedureIds.Add(proto.ID);
        }
    }

    private void OnAfterInteract(Entity<BodyComponent> target, ref AfterInteractUsingEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;

        var used = args.Used;
        var user = args.User;

        // No self-surgery
        if (user == target.Owner)
            return;

        // Draping tool on non-draped patient -> open procedure selection
        if (_tool.HasQuality(used, DrapingQuality)
            && !HasComp<SurgeryDrapedComponent>(target))
        {
            if (!_standing.IsDown(target.Owner))
            {
                _popup.PopupEntity(Loc.GetString("surgery-patient-not-down"), target, user);
                args.Handled = true;
                return;
            }

            // Don't drape yet; wait for procedure confirmation
            OpenProcedureMenu(user, target, used);
            args.Handled = true;
            return;
        }

        // Organ used on draped patient with active surgery -> insert directly
        if (HasComp<OrganComponent>(used) && HasComp<SurgeryDrapedComponent>(target) &&
            HasComp<ActiveSurgeryComponent>(target))
        {
            TryInsertOrgan(user, target, used);
            args.Handled = true;
            return;
        }

        // Tool used on draped patient -> surgery interaction
        if (!HasComp<ToolComponent>(used) || !HasComp<SurgeryDrapedComponent>(target))
            return;

        // Cautery universal close on active surgery
        if (IsCauteryTool(used) && HasComp<ActiveSurgeryComponent>(target))
        {
            StartCauteryClose(user, target, used);
            args.Handled = true;
            return;
        }

        // Active surgery: advance step
        if (TryComp<ActiveSurgeryComponent>(target, out var active) && active.ProcedureId != null)
        {
            TryAdvanceStep(user, target, used, active);
            args.Handled = true;
            return;
        }
    }

    private void OnDrapedStartup(Entity<SurgeryDrapedComponent> ent, ref ComponentStartup args)
    {
        _alerts.ShowAlert(ent.Owner, "SurgeryDraped");
    }

    private void OnRemoveDrapeAlert(Entity<SurgeryDrapedComponent> ent, ref RemoveSurgeryDrapeAlertEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        RemComp<ActiveSurgeryComponent>(ent);
        RemComp<SurgeryDrapedComponent>(ent); // Triggers OnDrapedShutdown -> drops bedsheet, clears alert
    }

    private void OpenProcedureMenu(EntityUid surgeon, EntityUid patient, EntityUid bedsheet)
    {
        if (_cachedProcedureIds.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("surgery-no-procedures"), patient, surgeon);
            return;
        }

        if (!TryComp<ActorComponent>(surgeon, out var actor))
            return;

        RaiseNetworkEvent(new OpenSurgeryMenuEvent(GetNetEntity(patient), GetNetEntity(bedsheet), _cachedProcedureIds), actor.PlayerSession);
    }

    private void OnProcedureSelected(SelectSurgeryProcedureEvent ev, EntitySessionEventArgs args)
    {
        // Validate sender
        if (args.SenderSession.AttachedEntity is not { } surgeon)
            return;

        if (!TryGetEntity(ev.Target, out var target))
            return;

        if (!TryGetEntity(ev.Bedsheet, out var bedsheet))
            return;

        if (!ProtoManager.TryIndex<SurgeryProcedurePrototype>(ev.ProcedureId, out var proto))
            return;

        // Validate: surgeon must be in range of patient
        if (!_interaction.InRangeUnobstructed(surgeon, target.Value))
            return;

        // No self-surgery
        if (surgeon == target.Value)
            return;

        // Validate: patient must still be down and not already draped
        if (HasComp<SurgeryDrapedComponent>(target.Value))
        {
            _popup.PopupEntity(Loc.GetString("surgery-already-draped"), target.Value, surgeon);
            return;
        }

        if (!_standing.IsDown(target.Value))
        {
            _popup.PopupEntity(Loc.GetString("surgery-patient-not-down"), target.Value, surgeon);
            return;
        }

        // Validate: drape item must still exist and have Draping quality
        if (!Exists(bedsheet.Value) || !_tool.HasQuality(bedsheet.Value, DrapingQuality))
        {
            _popup.PopupEntity(Loc.GetString("surgery-drape-missing"), target.Value, surgeon);
            return;
        }

        // Now drape the patient and take the bedsheet/drape
        var draped = EnsureComp<SurgeryDrapedComponent>(target.Value);
        draped.Bedsheet = bedsheet.Value;

        // Derive drape speed modifier from tier (standard = 1.0, no tier = 1.5 improvised)
        draped.DrapeSpeedModifier = GetToolTierModifier(bedsheet.Value);

        Dirty(target.Value, draped);

        var drapeContainer = _container.EnsureContainer<Container>(target.Value, "surgery_drape");
        if (!_container.Insert(bedsheet.Value, drapeContainer))
        {
            Log.Warning($"Failed to insert bedsheet {ToPrettyString(bedsheet.Value)} into surgery drape container on {ToPrettyString(target.Value)}");
            RemComp<SurgeryDrapedComponent>(target.Value);
            return;
        }

        _popup.PopupEntity(Loc.GetString("surgery-drape-patient", ("target", target.Value)), target.Value);

        var active = EnsureComp<ActiveSurgeryComponent>(target.Value);
        active.ProcedureId = ev.ProcedureId;
        active.CurrentStep = 0;
        active.Surgeon = surgeon;
        Dirty(target.Value, active);

        _popup.PopupEntity(
            Loc.GetString("surgery-procedure-started", ("procedure", Loc.GetString(proto.Name)), ("target", target.Value)),
            target.Value);
    }

    private void TryAdvanceStep(EntityUid surgeon, EntityUid patient, EntityUid tool, ActiveSurgeryComponent active)
    {
        if (active.ProcedureId == null ||
            !ProtoManager.TryIndex<SurgeryProcedurePrototype>(active.ProcedureId.Value, out var proto))
        {
            _popup.PopupEntity(Loc.GetString("surgery-procedure-invalid"), patient, surgeon);
            return;
        }

        if (active.CurrentStep >= proto.Steps.Count)
        {
            _popup.PopupEntity(Loc.GetString("surgery-procedure-complete"), patient, surgeon);
            return;
        }

        var currentStep = proto.Steps[active.CurrentStep];

        // Tool matches current step
        if (ToolMatchesStep(tool, currentStep))
        {
            StartStepDoAfter(surgeon, patient, tool, currentStep, proto.Difficulty);
            return;
        }

        // Advance past repeatable step if tool matches next step
        if (currentStep.Repeatable && active.CurrentStep + 1 < proto.Steps.Count)
        {
            var nextStep = proto.Steps[active.CurrentStep + 1];
            if (ToolMatchesStep(tool, nextStep))
            {
                active.CurrentStep++;
                Dirty(patient, active);
                StartStepDoAfter(surgeon, patient, tool, nextStep, proto.Difficulty);
                return;
            }
        }

        _popup.PopupEntity(Loc.GetString("surgery-wrong-tool"), patient, surgeon);
    }

    private void StartStepDoAfter(EntityUid surgeon, EntityUid patient, EntityUid tool, SurgeryStep step, SurgeryDifficulty difficulty)
    {
        var duration = TimeSpan.FromSeconds(
            (float) GetStepDuration(step, patient, difficulty).TotalSeconds
            * GetToolTierModifier(tool));

        var doAfterArgs = new DoAfterArgs(
            EntityManager,
            surgeon,
            duration,
            new SurgeryStepDoAfterEvent(),
            patient,
            target: patient,
            used: tool)
        {
            NeedHand = true,
            BreakOnMove = true,
            BreakOnHandChange = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfterArgs))
            _popup.PopupEntity(Loc.GetString("surgery-busy"), patient, surgeon);
    }

    private void StartCauteryClose(EntityUid surgeon, EntityUid patient, EntityUid tool)
    {
        var duration = TimeSpan.FromSeconds(2f
            * GetSurfaceSpeedModifier(patient)
            * GetDrapeSpeedModifier(patient)
            * GetToolTierModifier(tool));

        var doAfterArgs = new DoAfterArgs(
            EntityManager,
            surgeon,
            duration,
            new SurgeryCauteryDoAfterEvent(),
            patient,
            target: patient,
            used: tool)
        {
            NeedHand = true,
            BreakOnMove = true,
            BreakOnHandChange = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfterArgs))
            _popup.PopupEntity(Loc.GetString("surgery-busy"), patient, surgeon);
    }

    private void OnStepDoAfter(Entity<ActiveSurgeryComponent> ent, ref SurgeryStepDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        var patient = ent.Owner;
        var active = ent.Comp;

        if (active.ProcedureId == null ||
            !ProtoManager.TryIndex<SurgeryProcedurePrototype>(active.ProcedureId.Value, out var proto))
        {
            Log.Warning($"Surgery step DoAfter completed but procedure {active.ProcedureId} is invalid on {ToPrettyString(patient)}");
            return;
        }

        if (active.CurrentStep >= proto.Steps.Count)
        {
            _popup.PopupEntity(Loc.GetString("surgery-procedure-complete"), patient);
            return;
        }

        var step = proto.Steps[active.CurrentStep];

        // Snapshot damage before healing so we can detect zero-effect iterations.
        var damageBefore = _damageable.GetTotalDamage((patient, null));

        // Apply side effects
        ApplyStepEffects(patient, step);

        // Popup
        if (!string.IsNullOrEmpty(step.Popup) && args.User is { } user)
            _popup.PopupEntity(Loc.GetString(step.Popup, ("user", user), ("target", patient)), patient);

        // Trigger effect if this step has one
        if (step.Effect != null)
            HandleEffect(args.User, patient, step.Effect);

        // Advance step (unless repeatable).
        // Repeatable steps with effects (like organ manipulation) need manual re-use,
        // so only effect-less repeatable steps auto-repeat below.
        if (!step.Repeatable)
        {
            active.CurrentStep++;
            Dirty(patient, active);
        }
        else if (step.Effect == null)
        {
            var damageAfter = _damageable.GetTotalDamage((patient, null));
            var healedSomething = damageAfter < damageBefore;

            // Auto-repeat only if healing actually reduced damage.
            args.Repeat = healedSomething && StepCanStillHeal(patient, step);

            if (!args.Repeat)
                _popup.PopupEntity(Loc.GetString("surgery-step-repeat-done"), patient);
        }

        // Procedure steps exhausted, wait for cautery to close
        if (active.CurrentStep >= proto.Steps.Count)
            _popup.PopupEntity(Loc.GetString("surgery-procedure-complete"), patient);
    }

    private void OnCauteryDoAfter(Entity<ActiveSurgeryComponent> ent, ref SurgeryCauteryDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        ApplyCauteryClose(ent.Owner, args.User);
    }

    /// <summary>
    /// Returns a speed multiplier based on the tool's tier tag.
    /// No tier tag = improvised (1.5x).
    /// </summary>
    public float GetToolTierModifier(EntityUid tool)
    {
        if (_tags.HasTag(tool, TierExperimentalTag))
            return 0.7f;

        if (_tags.HasTag(tool, TierAdvancedTag))
            return 0.8f;

        if (_tags.HasTag(tool, TierStandardTag))
            return 1.0f;

        return 1.5f; // no tier tag = improvised
    }
}
