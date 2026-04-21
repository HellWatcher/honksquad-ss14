using System.Linq;
using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Server.Medical.Components;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
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
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction.Events;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.MedicalScanner;
using Content.Shared.RussStation.MedicalScanner;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using UpstreamHealthAnalyzerSystem = Content.Server.Medical.HealthAnalyzerSystem;

namespace Content.Server.RussStation.MedicalScanner;

/// <summary>
/// Reagent tab of the tabbed health analyzer UI. Drives the Reagents tab via
/// <see cref="HealthAnalyzerReagentScannerComponent"/>, which sits next to upstream's
/// <see cref="HealthAnalyzerComponent"/> so both systems can subscribe to the same
/// events without colliding in the (component, event) subscription slot.
///
/// Scans only fire for mobs: upstream's AfterInteract runs its DoAfter, and we piggyback
/// on <see cref="HealthAnalyzerDoAfterEvent"/> to push reagent state (bloodstream /
/// metabolites / stomachs / lungs) alongside the Health tab.
/// </summary>
public sealed class HealthAnalyzerReagentSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    private readonly Dictionary<string, ReagentDoseThresholds> _thresholdCache = new();

    public readonly record struct ReagentDoseThresholds(
        FixedPoint2? HarmfulMin,
        FixedPoint2? HarmfulMax,
        FixedPoint2? BeneficialMin);

    private enum EffectClass { Harmful, Beneficial, Neutral }

    public override void Initialize()
    {
        base.Initialize();

        // Piggyback upstream's DoAfter so one scan populates both Health and Reagents tabs.
        SubscribeLocalEvent<HealthAnalyzerReagentScannerComponent, HealthAnalyzerDoAfterEvent>(OnHealthDoAfter,
            after: new[] { typeof(UpstreamHealthAnalyzerSystem) });

        SubscribeLocalEvent<HealthAnalyzerReagentScannerComponent, DroppedEvent>(OnDropped);
        SubscribeLocalEvent<HealthAnalyzerReagentScannerComponent, EntGotInsertedIntoContainerMessage>(OnInsertedIntoContainer);
        SubscribeLocalEvent<HealthAnalyzerReagentScannerComponent, ItemToggledEvent>(OnToggled);

        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
    }

    public override void Update(float frameTime)
    {
        // Mirrors upstream HealthAnalyzerSystem.Update: rate limit, range-check, edge-paused on range exit.
        var query = EntityQueryEnumerator<HealthAnalyzerReagentScannerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var scanner, out var xform))
        {
            if (scanner.ReagentScanTarget is not { } target)
                continue;

            if (scanner.NextReagentUpdate > _timing.CurTime)
                continue;

            if (Deleted(target))
            {
                StopReagentScan((uid, scanner));
                continue;
            }

            scanner.NextReagentUpdate = _timing.CurTime + scanner.ReagentUpdateInterval;

            var targetCoords = Transform(target).Coordinates;
            if (scanner.MaxReagentScanRange != null
                && !_transform.InRange(targetCoords, xform.Coordinates, scanner.MaxReagentScanRange.Value))
            {
                PauseReagentScan((uid, scanner), target);
                continue;
            }

            scanner.IsReagentScanActive = true;
            Dirty(uid, scanner);
            PushReagentState(uid, target, active: true);
        }
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<ReagentPrototype>())
            _thresholdCache.Clear();
    }

    private void OnHealthDoAfter(Entity<HealthAnalyzerReagentScannerComponent> ent, ref HealthAnalyzerDoAfterEvent args)
    {
        // Upstream sets Handled on success; skip only if Cancelled / missing target.
        if (args.Cancelled || args.Target is not { } target)
            return;

        if (!_ui.HasUi(ent.Owner, HealthAnalyzerUiKey.Key))
            return;

        // Drop any prior scan pin so switching targets doesn't keep streaming the old mob.
        StopReagentScan(ent);

        // Only track live reagent updates if the mob actually exposes reagents worth watching.
        // PreferredTab = Health so a fresh scan defaults back to the Health tab even if the
        // player had switched to Reagents on a prior scan.
        if (!HasComp<BloodstreamComponent>(target))
        {
            PushEmptyReagentState(ent.Owner, target, preferredTab: HealthAnalyzerTab.Health);
            return;
        }

        ent.Comp.ReagentScanTarget = target;
        ent.Comp.NextReagentUpdate = _timing.CurTime + ent.Comp.ReagentUpdateInterval;
        ent.Comp.IsReagentScanActive = true;
        Dirty(ent);
        PushReagentState(ent.Owner, target, active: true, preferredTab: HealthAnalyzerTab.Health);
    }

    private void PushEmptyReagentState(EntityUid analyzer, EntityUid target, HealthAnalyzerTab? preferredTab = null)
    {
        if (!_ui.HasUi(analyzer, HealthAnalyzerUiKey.Key))
            return;

        var displayName = Identity.Name(target, EntityManager);
        var empty = new HealthAnalyzerReagentState(GetNetEntity(target), displayName,
            new List<HealthAnalyzerReagentGroup>(), active: true, preferredTab: preferredTab);
        _ui.SetUiState(analyzer, HealthAnalyzerUiKey.Key, empty);
    }

    private void PushReagentState(EntityUid analyzer, EntityUid target, bool active, HealthAnalyzerTab? preferredTab = null)
    {
        if (!_ui.HasUi(analyzer, HealthAnalyzerUiKey.Key))
            return;

        var state = BuildState(target);
        state.Active = active;
        state.PreferredTab = preferredTab;
        _ui.SetUiState(analyzer, HealthAnalyzerUiKey.Key, state);
    }

    private void PauseReagentScan(Entity<HealthAnalyzerReagentScannerComponent> ent, EntityUid target)
    {
        if (!ent.Comp.IsReagentScanActive)
            return;

        ent.Comp.IsReagentScanActive = false;
        Dirty(ent);
        PushReagentState(ent.Owner, target, active: false);
    }

    private void StopReagentScan(Entity<HealthAnalyzerReagentScannerComponent> ent)
    {
        ent.Comp.ReagentScanTarget = null;
        ent.Comp.IsReagentScanActive = false;
        Dirty(ent);
    }

    private void OnDropped(Entity<HealthAnalyzerReagentScannerComponent> ent, ref DroppedEvent args)
    {
        StopReagentScan(ent);
        if (_ui.HasUi(ent.Owner, HealthAnalyzerUiKey.Key))
            _ui.CloseUi(ent.Owner, HealthAnalyzerUiKey.Key);
    }

    private void OnInsertedIntoContainer(Entity<HealthAnalyzerReagentScannerComponent> ent, ref EntGotInsertedIntoContainerMessage args)
        => StopReagentScan(ent);

    private void OnToggled(Entity<HealthAnalyzerReagentScannerComponent> ent, ref ItemToggledEvent args)
    {
        if (!args.Activated)
            StopReagentScan(ent);
    }

    public HealthAnalyzerReagentState BuildState(EntityUid target)
    {
        var groups = new List<HealthAnalyzerReagentGroup>();

        if (TryComp<BloodstreamComponent>(target, out var bloodstream))
        {
            // Reagent OD/UD thresholds are calibrated against whole-reagent doses in the blood.
            // Metabolites are the trickle of in-progress metabolism output — their quantities
            // never reach those thresholds, so flagging them would be misleading noise. Only the
            // Blood group gets dose flags; metabolites / stomach / lung / puddle / container
            // entries pass false and render plain "{reagent}: Nu" without the dose chrome.
            AddSolution(groups, target, bloodstream.BloodSolutionName, ref bloodstream.BloodSolution,
                Loc.GetString("health-analyzer-reagent-group-blood"), showDoseFlags: true);
            AddSolution(groups, target, bloodstream.MetabolitesSolutionName, ref bloodstream.MetabolitesSolution,
                Loc.GetString("health-analyzer-reagent-group-metabolites"));

            AddOrganSolutions<StomachComponent>(groups, target,
                "health-analyzer-reagent-group-stomach",
                "health-analyzer-reagent-group-stomach-indexed",
                (uid, stomach) =>
                {
                    var handle = stomach.Solution;
                    return _solutions.ResolveSolution(uid, StomachSystem.DefaultSolutionName, ref handle, out var sol)
                        ? sol : null;
                });

            AddOrganSolutions<LungComponent>(groups, target,
                "health-analyzer-reagent-group-lung",
                "health-analyzer-reagent-group-lung-indexed",
                (uid, lung) =>
                {
                    // LungComponent is [Access]-locked to LungSystem; copy Solution into a local
                    // so ResolveSolution's ref parameter doesn't write back through the locked field.
                    var handle = lung.Solution;
                    return _solutions.ResolveSolution(uid, lung.SolutionName, ref handle, out var sol)
                        ? sol : null;
                });
        }

        var displayName = Identity.Name(target, EntityManager);
        return new HealthAnalyzerReagentState(GetNetEntity(target), displayName, groups);
    }

    private void AddOrganSolutions<TOrgan>(
        List<HealthAnalyzerReagentGroup> groups,
        EntityUid body,
        string singleKey,
        string indexedKey,
        Func<EntityUid, TOrgan, Solution?> resolve)
        where TOrgan : IComponent
    {
        if (!TryComp<BodyComponent>(body, out var bodyComp) || bodyComp.Organs is null)
            return;

        var organs = new List<(EntityUid Uid, TOrgan Comp)>();
        foreach (var organ in bodyComp.Organs.ContainedEntities)
        {
            if (TryComp<TOrgan>(organ, out var comp))
                organs.Add((organ, comp));
        }

        for (var i = 0; i < organs.Count; i++)
        {
            var (uid, comp) = organs[i];
            var label = organs.Count > MedicalScannerConstants.MultiOrganLabelThreshold
                ? Loc.GetString(indexedKey, ("index", i + MedicalScannerConstants.OrganIndexLabelOffset))
                : Loc.GetString(singleKey);
            if (resolve(uid, comp) is { } sol)
                AddSolutionFromSolution(groups, sol, label);
        }
    }

    private void AddSolution(List<HealthAnalyzerReagentGroup> groups, EntityUid owner, string name,
        ref Entity<SolutionComponent>? handle, string label, bool showDoseFlags = false)
    {
        if (!_solutions.ResolveSolution(owner, name, ref handle, out var solution))
            return;

        AddSolutionFromSolution(groups, solution, label, showDoseFlags);
    }

    private void AddSolutionFromSolution(List<HealthAnalyzerReagentGroup> groups, Solution solution, string label, bool showDoseFlags = false)
    {
        var entries = new List<HealthAnalyzerReagentEntry>(solution.Contents.Count);
        foreach (var (id, qty) in solution.Contents
                     .Select(rq => (rq.Reagent.Prototype, rq.Quantity))
                     .OrderByDescending(t => t.Quantity))
        {
            if (!_proto.TryIndex<ReagentPrototype>(id, out var protoData))
                continue;

            var od = false;
            var ud = false;
            if (showDoseFlags)
            {
                var thresholds = GetDoseThresholds(protoData);
                od = (thresholds.HarmfulMin.HasValue && qty >= thresholds.HarmfulMin.Value)
                     || (thresholds.HarmfulMax.HasValue && qty <= thresholds.HarmfulMax.Value);
                ud = !od && thresholds.BeneficialMin.HasValue && qty < thresholds.BeneficialMin.Value;
            }
            entries.Add(new HealthAnalyzerReagentEntry(id, protoData.LocalizedName, protoData.SubstanceColor, qty, od, ud));
        }

        groups.Add(new HealthAnalyzerReagentGroup(label, solution.Volume, solution.MaxVolume, entries));
    }

    /// <summary>
    /// Walks a reagent's metabolisms looking for self-referencing <see cref="ReagentCondition"/>s
    /// and buckets the bounds into harmful or beneficial thresholds based on the effect type.
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
            if (rc.Inverted)
                continue;

            if (rc.Min > FixedPoint2.Zero && (min is null || rc.Min < min.Value))
                min = rc.Min;
            if (rc.Max < FixedPoint2.MaxValue && (max is null || rc.Max > max.Value))
                max = rc.Max;
        }
        return (min, max);
    }

    private static EffectClass ClassifyEffect(EntityEffect effect, string reagentId)
    {
        switch (effect)
        {
            case HealthChange hc:
                return ClassifyDamageValues(hc.Damage.DamageDict.Values);
            case EvenHealthChange ehc:
                return ClassifyDamageValues(ehc.Damage.Values);

            case AdjustReagent ar:
                if (ar.Reagent == reagentId)
                    return ar.Amount < FixedPoint2.Zero ? EffectClass.Neutral : EffectClass.Harmful;
                return EffectClass.Neutral;

            case MovementSpeedModifier msm:
                if (msm.WalkSpeedModifier < MedicalScannerConstants.NeutralMovementSpeedModifier
                    || msm.SprintSpeedModifier < MedicalScannerConstants.NeutralMovementSpeedModifier)
                    return EffectClass.Harmful;
                if (msm.WalkSpeedModifier > MedicalScannerConstants.NeutralMovementSpeedModifier
                    || msm.SprintSpeedModifier > MedicalScannerConstants.NeutralMovementSpeedModifier)
                    return EffectClass.Beneficial;
                return EffectClass.Neutral;

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
