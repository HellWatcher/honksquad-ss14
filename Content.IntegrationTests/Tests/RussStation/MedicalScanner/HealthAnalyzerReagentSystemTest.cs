using System.Linq;
using Content.Server.RussStation.MedicalScanner;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.RussStation.MedicalScanner;

[TestFixture]
[TestOf(typeof(HealthAnalyzerReagentSystem))]
public sealed class HealthAnalyzerReagentSystemTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: HealthAnalyzerReagentTestBeaker
  components:
  - type: SolutionContainerManager
    solutions:
      beaker:
        maxVol: 50

# Synthetic reagent whose only self-gated effect is a healing HealthChange,
# so its threshold should be classified as beneficial (UD when below).
- type: reagent
  id: TestUnderdoseReagent
  name: reagent-name-nothing
  desc: reagent-desc-nothing
  physicalDesc: reagent-physical-desc-nothing
  metabolisms:
    Bloodstream:
      effects:
      - !type:HealthChange
        conditions:
        - !type:ReagentCondition
          reagent: TestUnderdoseReagent
          min: 8
        damage:
          types:
            Blunt: -2

# Synthetic reagent whose only self-gated effects are an emote and a popup, both
# pure flavor. The classifier should treat both as neutral and produce no
# thresholds at all (matching how Happiness should behave).
- type: reagent
  id: TestNeutralFlavorReagent
  name: reagent-name-nothing
  desc: reagent-desc-nothing
  physicalDesc: reagent-physical-desc-nothing
  metabolisms:
    Bloodstream:
      effects:
      - !type:Emote
        emote: Laugh
        conditions:
        - !type:ReagentCondition
          reagent: TestNeutralFlavorReagent
          min: 5
      - !type:PopupMessage
        type: Local
        messages: [ ""carpetium-effect-blood-fibrous"" ]
        conditions:
        - !type:ReagentCondition
          reagent: TestNeutralFlavorReagent
          min: 5

# Synthetic reagent that is harmful when too low (slow movement gated by max:)
# AND harmful when too high (HealthChange gated by min:). Models the Fresium
# 'no good range' pattern. Should OD-flag at every nonzero quantity.
- type: reagent
  id: TestRangeHarmfulReagent
  name: reagent-name-nothing
  desc: reagent-desc-nothing
  physicalDesc: reagent-physical-desc-nothing
  metabolisms:
    Bloodstream:
      effects:
      - !type:MovementSpeedModifier
        conditions:
        - !type:ReagentCondition
          reagent: TestRangeHarmfulReagent
          max: 10
        walkSpeedModifier: 0.5
        sprintSpeedModifier: 0.5
      - !type:HealthChange
        conditions:
        - !type:ReagentCondition
          reagent: TestRangeHarmfulReagent
          min: 20
        damage:
          types:
            Poison: 1

# Synthetic reagent whose only self-gated effect is AdjustReagent decaying
# itself. Should be classified neutral (just metabolism speed-up, not harm).
- type: reagent
  id: TestSelfDecayReagent
  name: reagent-name-nothing
  desc: reagent-desc-nothing
  physicalDesc: reagent-physical-desc-nothing
  metabolisms:
    Bloodstream:
      effects:
      - !type:AdjustReagent
        reagent: TestSelfDecayReagent
        amount: -5
        conditions:
        - !type:ReagentCondition
          reagent: TestSelfDecayReagent
          min: 1
";

    [Test]
    public async Task BicaridineBelowAndAboveOverdoseFlagged()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var containerSystem = entityManager.System<SharedSolutionContainerSystem>();
        var system = entityManager.System<HealthAnalyzerReagentSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var beaker = entityManager.SpawnEntity("HealthAnalyzerReagentTestBeaker", mapData.GridCoords);
            Assert.That(containerSystem.TryGetSolution(beaker, "beaker", out var solutionEnt, out _));

            // Below OD threshold (Bicaridine min:15 in YAML, gating harmful Asphyxiation/Poison).
            containerSystem.TryAddSolution(solutionEnt!.Value, new Solution("Bicaridine", FixedPoint2.New(10)));

            var stateBelow = system.BuildState(beaker);
            var groupBelow = stateBelow.Groups.Single();
            var entryBelow = groupBelow.Reagents.Single(r => r.ReagentId == "Bicaridine");
            Assert.That(entryBelow.Quantity, Is.EqualTo(FixedPoint2.New(10)));
            Assert.That(entryBelow.Overdose, Is.False, "Bicaridine at 10u should not be flagged as overdose.");
            Assert.That(entryBelow.Underdose, Is.False, "Bicaridine has no beneficial dose gate; should never be UD.");

            // Top up past OD threshold.
            containerSystem.TryAddSolution(solutionEnt.Value, new Solution("Bicaridine", FixedPoint2.New(10)));

            var stateAbove = system.BuildState(beaker);
            var groupAbove = stateAbove.Groups.Single();
            var entryAbove = groupAbove.Reagents.Single(r => r.ReagentId == "Bicaridine");
            Assert.That(entryAbove.Quantity, Is.EqualTo(FixedPoint2.New(20)));
            Assert.That(entryAbove.Overdose, Is.True, "Bicaridine at 20u should be flagged as overdose.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BeneficialGatedReagentFlagsUnderdose()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var containerSystem = entityManager.System<SharedSolutionContainerSystem>();
        var system = entityManager.System<HealthAnalyzerReagentSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var beaker = entityManager.SpawnEntity("HealthAnalyzerReagentTestBeaker", mapData.GridCoords);
            Assert.That(containerSystem.TryGetSolution(beaker, "beaker", out var solutionEnt, out _));

            // Below the beneficial activation threshold (TestUnderdoseReagent min:8).
            containerSystem.TryAddSolution(solutionEnt!.Value, new Solution("TestUnderdoseReagent", FixedPoint2.New(5)));

            var stateBelow = system.BuildState(beaker);
            var entryBelow = stateBelow.Groups.Single().Reagents.Single(r => r.ReagentId == "TestUnderdoseReagent");
            Assert.That(entryBelow.Underdose, Is.True, "Beneficial reagent below activation min should be UD.");
            Assert.That(entryBelow.Overdose, Is.False);

            // At the threshold the beneficial effect is active; no flag either way.
            containerSystem.TryAddSolution(solutionEnt.Value, new Solution("TestUnderdoseReagent", FixedPoint2.New(5)));

            var stateActive = system.BuildState(beaker);
            var entryActive = stateActive.Groups.Single().Reagents.Single(r => r.ReagentId == "TestUnderdoseReagent");
            Assert.That(entryActive.Quantity, Is.EqualTo(FixedPoint2.New(10)));
            Assert.That(entryActive.Underdose, Is.False, "Beneficial reagent at/above activation should not be UD.");
            Assert.That(entryActive.Overdose, Is.False);
        });

        await pair.CleanReturnAsync();
    }

    private static readonly ProtoId<ReagentPrototype> Bicaridine = "Bicaridine";

    [Test]
    public async Task OverdoseHeuristicMatchesYamlForBicaridine()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var entityManager = server.ResolveDependency<IEntityManager>();
        var system = entityManager.System<HealthAnalyzerReagentSystem>();

        await server.WaitAssertion(() =>
        {
            var bicaridine = protoMan.Index(Bicaridine);
            var thresholds = system.GetDoseThresholds(bicaridine);
            Assert.That(thresholds.HarmfulMin, Is.Not.Null, "Expected Bicaridine to have a harmful dose threshold from its metabolism conditions.");
            Assert.That(thresholds.HarmfulMin!.Value, Is.EqualTo(FixedPoint2.New(15)));
            Assert.That(thresholds.HarmfulMax, Is.Null, "Bicaridine has no self-gated max threshold.");
            Assert.That(thresholds.BeneficialMin, Is.Null, "Bicaridine has no self-gated beneficial effect.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PureFlavorReagentNeverFlagged()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var containerSystem = entityManager.System<SharedSolutionContainerSystem>();
        var system = entityManager.System<HealthAnalyzerReagentSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var beaker = entityManager.SpawnEntity("HealthAnalyzerReagentTestBeaker", mapData.GridCoords);
            Assert.That(containerSystem.TryGetSolution(beaker, "beaker", out var solutionEnt, out _));

            // Well above the gating min — but the gated effects are pure flavor, so neither flag should fire.
            containerSystem.TryAddSolution(solutionEnt!.Value, new Solution("TestNeutralFlavorReagent", FixedPoint2.New(40)));

            var state = system.BuildState(beaker);
            var entry = state.Groups.Single().Reagents.Single(r => r.ReagentId == "TestNeutralFlavorReagent");
            Assert.That(entry.Overdose, Is.False, "Pure-flavor (Emote/PopupMessage) reagents must not OD-flag.");
            Assert.That(entry.Underdose, Is.False, "Pure-flavor reagents must not UD-flag.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RangeHarmfulReagentFlagsBothLowAndHigh()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var containerSystem = entityManager.System<SharedSolutionContainerSystem>();
        var system = entityManager.System<HealthAnalyzerReagentSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var beaker = entityManager.SpawnEntity("HealthAnalyzerReagentTestBeaker", mapData.GridCoords);
            Assert.That(containerSystem.TryGetSolution(beaker, "beaker", out var solutionEnt, out _));

            // Inside the harmful-low range (max:10 walkSpeedModifier 0.5).
            containerSystem.TryAddSolution(solutionEnt!.Value, new Solution("TestRangeHarmfulReagent", FixedPoint2.New(5)));
            var stateLow = system.BuildState(beaker);
            var entryLow = stateLow.Groups.Single().Reagents.Single(r => r.ReagentId == "TestRangeHarmfulReagent");
            Assert.That(entryLow.Overdose, Is.True, "Below the max:10 slow gate, the harmful effect is active — should OD.");

            // Push past the harmful-high gate (min:20 Poison).
            containerSystem.TryAddSolution(solutionEnt.Value, new Solution("TestRangeHarmfulReagent", FixedPoint2.New(20)));
            var stateHigh = system.BuildState(beaker);
            var entryHigh = stateHigh.Groups.Single().Reagents.Single(r => r.ReagentId == "TestRangeHarmfulReagent");
            Assert.That(entryHigh.Quantity, Is.EqualTo(FixedPoint2.New(25)));
            Assert.That(entryHigh.Overdose, Is.True, "Above the min:20 poison gate, the harmful effect is active — should OD.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SelfDecayReagentNotFlaggedHarmful()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var containerSystem = entityManager.System<SharedSolutionContainerSystem>();
        var system = entityManager.System<HealthAnalyzerReagentSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var beaker = entityManager.SpawnEntity("HealthAnalyzerReagentTestBeaker", mapData.GridCoords);
            Assert.That(containerSystem.TryGetSolution(beaker, "beaker", out var solutionEnt, out _));

            // Well above the self-decay min:1 — should NOT OD-flag because self-decay is metabolism speed-up, not harm.
            containerSystem.TryAddSolution(solutionEnt!.Value, new Solution("TestSelfDecayReagent", FixedPoint2.New(20)));

            var state = system.BuildState(beaker);
            var entry = state.Groups.Single().Reagents.Single(r => r.ReagentId == "TestSelfDecayReagent");
            Assert.That(entry.Overdose, Is.False, "Self-decaying AdjustReagent (negative amount) must not OD-flag.");
            Assert.That(entry.Underdose, Is.False);
        });

        await pair.CleanReturnAsync();
    }
}
