using System.Linq;
using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.RussStation.MedicalScanner;
using Robust.Shared.Prototypes;

namespace Content.Server.RussStation.MedicalScanner;

/// <summary>
/// Gathers the reagent solutions worth surfacing on a scanned mob — bloodstream, metabolites,
/// stomach and lung contents — into the <see cref="HealthAnalyzerReagentGroup"/> list rendered
/// by the Reagents tab. Pulled out of <see cref="HealthAnalyzerReagentSystem"/> so the
/// solution-walking logic lives apart from the scanner lifecycle/UI plumbing, leaving
/// <see cref="HealthAnalyzerReagentSystem.BuildState"/> as a thin wrapper.
///
/// Overdose/underdose flags are sourced from <see cref="ReagentDoseThresholdAnalyzer"/>.
/// </summary>
public sealed class SolutionAggregator : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly ReagentDoseThresholdAnalyzer _thresholds = default!;

    /// <summary>
    /// Builds the ordered reagent group list for <paramref name="target"/>. Mobs without a
    /// <see cref="BloodstreamComponent"/> produce an empty list.
    /// </summary>
    public List<HealthAnalyzerReagentGroup> BuildGroups(EntityUid target)
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

        return groups;
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
                var thresholds = _thresholds.GetDoseThresholds(protoData);
                od = (thresholds.HarmfulMin.HasValue && qty >= thresholds.HarmfulMin.Value)
                     || (thresholds.HarmfulMax.HasValue && qty <= thresholds.HarmfulMax.Value);
                ud = !od && thresholds.BeneficialMin.HasValue && qty < thresholds.BeneficialMin.Value;
            }
            entries.Add(new HealthAnalyzerReagentEntry(id, protoData.LocalizedName, protoData.SubstanceColor, qty, od, ud));
        }

        groups.Add(new HealthAnalyzerReagentGroup(label, solution.Volume, solution.MaxVolume, entries));
    }
}
