using System.Linq;
using Content.Server.Body.Components;
using Content.Server.Medical.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.EntityConditions;
using Content.Shared.EntityConditions.Conditions;
using Content.Shared.EntityEffects;
using Content.Shared.EntityEffects.Effects;
using Content.Shared.EntityEffects.Effects.Damage;
using Content.Shared.EntityEffects.Effects.Solution;
using Content.Shared.EntityEffects.Effects.StatusEffects;
using Content.Shared.EntityEffects.Effects.Transform;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.MedicalScanner;
using Content.Shared.PowerCell;
using Content.Shared.RussStation.MedicalScanner;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server.RussStation.MedicalScanner;

/// <summary>
/// Adds an alt-verb on bloodstream / puddle / solution-container entities that
/// runs a do-after and opens a separate Health Analyzer UI listing reagents
/// (with an overdose flag).
/// </summary>
public sealed class HealthAnalyzerReagentSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly PowerCellSystem _cell = default!;

    // Cache of reagent id -> dose thresholds derived from self-referencing ReagentConditions.
    // Each effect's Min is bucketed as either harmful or beneficial based on the effect type.
    private readonly Dictionary<string, ReagentDoseThresholds> _thresholdCache = new();

    public readonly record struct ReagentDoseThresholds(
        FixedPoint2? HarmfulMin,
        FixedPoint2? HarmfulMax,
        FixedPoint2? BeneficialMin);

    private enum EffectClass { Harmful, Beneficial, Neutral }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BloodstreamComponent, GetVerbsEvent<AlternativeVerb>>(OnGetMobVerbs);
        SubscribeLocalEvent<PuddleComponent, GetVerbsEvent<AlternativeVerb>>(OnGetSolutionVerbs);
        SubscribeLocalEvent<SolutionContainerManagerComponent, GetVerbsEvent<AlternativeVerb>>(OnGetSolutionVerbs);

        SubscribeLocalEvent<HealthAnalyzerComponent, HealthAnalyzerReagentDoAfterEvent>(OnDoAfter);

        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<ReagentPrototype>())
            _thresholdCache.Clear();
    }

    private void OnGetMobVerbs(EntityUid uid, BloodstreamComponent comp, GetVerbsEvent<AlternativeVerb> args)
        => TryAddVerb(uid, args);

    private void OnGetSolutionVerbs<T>(EntityUid uid, T comp, GetVerbsEvent<AlternativeVerb> args) where T : Component
        => TryAddVerb(uid, args);

    private void TryAddVerb(EntityUid target, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        if (args.Using is not { } analyzer || !TryComp<HealthAnalyzerComponent>(analyzer, out var analyzerComp))
            return;

        var user = args.User;
        var targetCapture = target;

        var verb = new AlternativeVerb
        {
            Text = Loc.GetString("health-analyzer-verb-scan-reagents"),
            Icon = new Robust.Shared.Utility.SpriteSpecifier.Texture(new Robust.Shared.Utility.ResPath("/Textures/Interface/VerbIcons/dot.svg.192dpi.png")),
            Act = () => StartScan(user, (analyzer, analyzerComp), targetCapture),
        };
        args.Verbs.Add(verb);
    }

    private void StartScan(EntityUid user, Entity<HealthAnalyzerComponent> analyzer, EntityUid target)
    {
        if (!_cell.HasDrawCharge(analyzer.Owner, user: user))
            return;

        _audio.PlayPvs(analyzer.Comp.ScanningBeginSound, analyzer);

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, analyzer.Comp.ScanDelay,
            new HealthAnalyzerReagentDoAfterEvent(), analyzer, target: target, used: analyzer)
        {
            NeedHand = true,
            BreakOnMove = true,
        });
    }

    private void OnDoAfter(Entity<HealthAnalyzerComponent> analyzer, ref HealthAnalyzerReagentDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } target)
            return;

        if (!_cell.HasDrawCharge(analyzer.Owner, user: args.User))
            return;

        if (!analyzer.Comp.Silent)
            _audio.PlayPvs(analyzer.Comp.ScanningEndSound, analyzer);

        if (!_ui.HasUi(analyzer.Owner, HealthAnalyzerUiKey.Reagents))
        {
            args.Handled = true;
            return;
        }

        var state = BuildState(target);
        _ui.OpenUi(analyzer.Owner, HealthAnalyzerUiKey.Reagents, args.User);
        _ui.ServerSendUiMessage(analyzer.Owner, HealthAnalyzerUiKey.Reagents,
            new HealthAnalyzerReagentScannedMessage(state), args.User);

        args.Handled = true;
    }

    public HealthAnalyzerReagentState BuildState(EntityUid target)
    {
        var groups = new List<HealthAnalyzerReagentGroup>();

        if (TryComp<BloodstreamComponent>(target, out var bloodstream))
        {
            AddSolution(groups, target, bloodstream.BloodSolutionName, ref bloodstream.BloodSolution,
                Loc.GetString("health-analyzer-reagent-group-blood"));
            AddSolution(groups, target, bloodstream.MetabolitesSolutionName, ref bloodstream.MetabolitesSolution,
                Loc.GetString("health-analyzer-reagent-group-metabolites"));

            // Stomachs live on body parts. Walk the body and find all StomachComponents.
            var stomachIndex = 1;
            var stomachQuery = EntityQueryEnumerator<StomachComponent, Robust.Shared.GameObjects.TransformComponent>();
            while (stomachQuery.MoveNext(out var stomachUid, out var stomach, out var xform))
            {
                if (!IsOwnedBy(stomachUid, target))
                    continue;

                var label = Loc.GetString("health-analyzer-reagent-group-stomach", ("index", stomachIndex++));
                AddSolution(groups, stomachUid, StomachSystem.DefaultSolutionName, ref stomach.Solution, label);
            }
        }

        if (TryComp<PuddleComponent>(target, out var puddle))
        {
            AddSolution(groups, target, puddle.SolutionName, ref puddle.Solution,
                Loc.GetString("health-analyzer-reagent-group-puddle"));
        }
        else if (HasComp<SolutionContainerManagerComponent>(target))
        {
            // Generic solution container (drink, beaker, pill bottle, ...)
            foreach (var (solutionName, soln) in _solutions.EnumerateSolutions(target))
            {
                var label = string.IsNullOrEmpty(solutionName)
                    ? Loc.GetString("health-analyzer-reagent-group-container-unnamed")
                    : Loc.GetString("health-analyzer-reagent-group-container-named", ("name", solutionName!));
                AddSolutionFromEntity(groups, soln, label);
            }
        }

        var displayName = Identity.Name(target, EntityManager);
        return new HealthAnalyzerReagentState(GetNetEntity(target), displayName, groups);
    }

    private bool IsOwnedBy(EntityUid child, EntityUid root)
    {
        var xform = Transform(child);
        var parent = xform.ParentUid;
        while (parent.IsValid())
        {
            if (parent == root)
                return true;
            parent = Transform(parent).ParentUid;
        }
        return false;
    }

    private void AddSolution(List<HealthAnalyzerReagentGroup> groups, EntityUid owner, string name,
        ref Entity<SolutionComponent>? handle, string label)
    {
        if (!_solutions.ResolveSolution(owner, name, ref handle, out var solution))
            return;

        AddSolutionFromSolution(groups, solution, label);
    }

    private void AddSolutionFromEntity(List<HealthAnalyzerReagentGroup> groups,
        Entity<SolutionComponent> handle, string label)
    {
        AddSolutionFromSolution(groups, handle.Comp.Solution, label);
    }

    private void AddSolutionFromSolution(List<HealthAnalyzerReagentGroup> groups, Solution solution, string label)
    {
        if (solution.Volume <= FixedPoint2.Zero)
            return;

        var entries = new List<HealthAnalyzerReagentEntry>(solution.Contents.Count);
        foreach (var (id, qty) in solution.Contents
                     .Select(rq => (rq.Reagent.Prototype, rq.Quantity))
                     .OrderByDescending(t => t.Quantity))
        {
            if (!_proto.TryIndex<ReagentPrototype>(id, out var protoData))
                continue;

            var thresholds = GetDoseThresholds(protoData);
            // OD: a harmful effect is currently active. Either the quantity is at or above
            // a min-gated harmful threshold, or at or below a max-gated harmful threshold
            // (the "harmful while you have too little" pattern, e.g. Fresium freezing your insides).
            var od = (thresholds.HarmfulMin.HasValue && qty >= thresholds.HarmfulMin.Value)
                  || (thresholds.HarmfulMax.HasValue && qty <= thresholds.HarmfulMax.Value);
            // UD: a beneficial gating threshold exists and the current quantity is below it.
            // OD takes precedence so we don't double-flag.
            var ud = !od && thresholds.BeneficialMin.HasValue && qty < thresholds.BeneficialMin.Value;
            entries.Add(new HealthAnalyzerReagentEntry(id, protoData.LocalizedName, protoData.SubstanceColor, qty, od, ud));
        }

        groups.Add(new HealthAnalyzerReagentGroup(label, solution.Volume, solution.MaxVolume, entries));
    }

    /// <summary>
    /// Walks a reagent's metabolisms looking for self-referencing <see cref="ReagentCondition"/>s
    /// (i.e. the reagent gating its own effects by quantity) and buckets the bounds into
    /// harmful or beneficial thresholds based on the effect type. Tracks both <c>min:</c> and
    /// <c>max:</c> on harmful effects so "ideal range" reagents (e.g. Fresium, harmful both
    /// when too low and too high) get flagged in either direction. The classifier recognises
    /// pure-flavor effect types (emotes, popups, status effects, self-decay) as neutral so
    /// reagents like Happiness — whose only gated effects are emotes — don't get false-flagged.
    ///
    /// Limitations: ignores <see cref="NestedCondition"/> wrappers, ignores cross-reagent gating,
    /// ignores inverted <see cref="ReagentCondition"/>s, and doesn't track BeneficialMax (a
    /// beneficial effect that fires only below a cap is rare and the UI has no good word for it).
    /// </summary>
    public ReagentDoseThresholds GetDoseThresholds(ReagentPrototype proto)
    {
        if (_thresholdCache.TryGetValue(proto.ID, out var cached))
            return cached;

        FixedPoint2? harmfulMin = null;
        FixedPoint2? harmfulMax = null;
        FixedPoint2? beneficialMin = null;

        if (proto.Metabolisms != null)
        {
            foreach (var (_, entry) in proto.Metabolisms.Metabolisms)
            {
                foreach (var effect in entry.Effects)
                {
                    if (effect.Conditions == null)
                        continue;

                    var cls = ClassifyEffect(effect, proto.ID);
                    if (cls == EffectClass.Neutral)
                        continue;

                    var (selfMin, selfMax) = SelfBounds(proto.ID, effect.Conditions);
                    if (selfMin is null && selfMax is null)
                        continue;

                    if (cls == EffectClass.Beneficial)
                    {
                        if (selfMin is { } bMin && (beneficialMin is null || bMin < beneficialMin.Value))
                            beneficialMin = bMin;
                    }
                    else
                    {
                        if (selfMin is { } hMin && (harmfulMin is null || hMin < harmfulMin.Value))
                            harmfulMin = hMin;
                        // Take the largest max across harmful effects so the union of
                        // "harmful below" ranges covers any qty <= harmfulMax.
                        if (selfMax is { } hMax && (harmfulMax is null || hMax > harmfulMax.Value))
                            harmfulMax = hMax;
                    }
                }
            }
        }

        var result = new ReagentDoseThresholds(harmfulMin, harmfulMax, beneficialMin);
        _thresholdCache[proto.ID] = result;
        return result;
    }

    private static (FixedPoint2? Min, FixedPoint2? Max) SelfBounds(string reagentId, EntityCondition[] conditions)
    {
        FixedPoint2? min = null;
        FixedPoint2? max = null;
        foreach (var cond in conditions)
        {
            if (cond is not ReagentCondition rc)
                continue;
            if (rc.Reagent != reagentId)
                continue;
            // Inverted ReagentConditions flip the meaning; rather than guess, skip them.
            if (rc.Inverted)
                continue;

            if (rc.Min > FixedPoint2.Zero && (min is null || rc.Min < min.Value))
                min = rc.Min;
            if (rc.Max < FixedPoint2.MaxValue && (max is null || rc.Max > max.Value))
                max = rc.Max;
        }
        return (min, max);
    }

    /// <summary>
    /// Buckets an effect into harmful, beneficial, or neutral. The default is harmful so
    /// uncategorised side-effect types (Vomit, Drunk, Jitter, Polymorph, …) still get OD-flagged,
    /// which matches the YAML tendency for self-gated effects to be downsides. Specific types
    /// known to carry no inherent classification (pure-flavor emotes, popups, opaque status
    /// effects, self-decay) opt out as neutral so they don't trigger false positives.
    /// </summary>
    private static EffectClass ClassifyEffect(EntityEffect effect, string reagentId)
    {
        switch (effect)
        {
            case HealthChange hc:
                return ClassifyDamageValues(hc.Damage.DamageDict.Values);
            case EvenHealthChange ehc:
                return ClassifyDamageValues(ehc.Damage.Values);

            case AdjustReagent ar:
                // Self-targeted decay (negative amount) is metabolism speed-up, not harm.
                // Self-targeted accumulation is harmful (the reagent makes more of itself).
                // Cross-reagent adjustments are too context-dependent to classify here.
                if (ar.Reagent == reagentId)
                    return ar.Amount < FixedPoint2.Zero ? EffectClass.Neutral : EffectClass.Harmful;
                return EffectClass.Neutral;

            case MovementSpeedModifier msm:
                if (msm.WalkSpeedModifier < 1f || msm.SprintSpeedModifier < 1f)
                    return EffectClass.Harmful;
                if (msm.WalkSpeedModifier > 1f || msm.SprintSpeedModifier > 1f)
                    return EffectClass.Beneficial;
                return EffectClass.Neutral;

            // Pure flavor / opaque effects: no inherent harm or benefit we can read from
            // the YAML alone. Resolving the referenced status proto would mean a deeper walk
            // and is rarely worth it — defaulting to neutral avoids false-positive ODs.
            case PopupMessage:
            case Emote:
            case GenericStatusEffect:
            case ModifyStatusEffect:
                return EffectClass.Neutral;

            default:
                return EffectClass.Harmful;
        }
    }

    private static EffectClass ClassifyDamageValues(IEnumerable<FixedPoint2> values)
    {
        var anyPositive = false;
        var anyNegative = false;
        foreach (var v in values)
        {
            if (v > FixedPoint2.Zero)
                anyPositive = true;
            else if (v < FixedPoint2.Zero)
                anyNegative = true;
        }
        if (anyPositive)
            return EffectClass.Harmful;
        if (anyNegative)
            return EffectClass.Beneficial;
        return EffectClass.Neutral;
    }
}
