using System.Linq;
using Content.Server.RussStation.MedicalScanner;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.RussStation.MedicalScanner;

[TestFixture]
[TestOf(typeof(HealthAnalyzerReagentSystem))]
public sealed class HealthAnalyzerReagentSystemTest
{
    [TestPrototypes]
    private const string Prototypes = @"
# Minimal dummy with a bloodstream so BuildState's mob branch has something to populate.
# BloodstreamSystem.OnComponentInit creates and pre-fills the bloodstream/metabolites
# solutions, so the test can drop Bicaridine straight in without extra scaffolding.
- type: entity
  id: HealthAnalyzerReagentMobDummy
  components:
  - type: SolutionContainerManager
  - type: Bloodstream
    bloodlossDamage:
      types:
        Bloodloss: 1
    bloodlossHealDamage:
      types:
        Bloodloss: -1

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
# 'no good range' pattern. Should produce both a HarmfulMin and a HarmfulMax.
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

    private static readonly ProtoId<ReagentPrototype> Bicaridine = "Bicaridine";

    // Test-only reagents from [TestPrototypes] above. Kept as `const string` (not
    // ProtoId<T>) so the YAML linter skips them; passing via a named const also
    // satisfies RA0033, which only rejects inline string literals.
    private const string TestUnderdoseReagent = "TestUnderdoseReagent";
    private const string TestNeutralFlavorReagent = "TestNeutralFlavorReagent";
    private const string TestRangeHarmfulReagent = "TestRangeHarmfulReagent";
    private const string TestSelfDecayReagent = "TestSelfDecayReagent";

    [Test]
    public async Task MobScanPopulatesBloodGroupWithOverdoseFlag()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var containerSystem = entityManager.System<SharedSolutionContainerSystem>();
        var system = entityManager.System<HealthAnalyzerReagentSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var mob = entityManager.SpawnEntity("HealthAnalyzerReagentMobDummy", mapData.GridCoords);
            var bloodstream = entityManager.GetComponent<BloodstreamComponent>(mob);
            Assert.That(containerSystem.TryGetSolution(mob, bloodstream.BloodSolutionName,
                out var bloodHandle, out _), "BloodstreamSystem should have created the blood solution on init.");

            // Push past Bicaridine's 15u OD threshold. 20u is enough margin to leave the
            // pre-filled blood below its own cap even after the BLOOD reference fills it.
            containerSystem.TryAddSolution(bloodHandle!.Value, new Solution("Bicaridine", FixedPoint2.New(20)));

            var state = system.BuildState(mob);
            var bloodGroup = state.Groups.Single(g => g.Label == Loc.GetString("health-analyzer-reagent-group-blood"));
            var bicaridine = bloodGroup.Reagents.Single(r => r.ReagentId == "Bicaridine");

            Assert.That(bicaridine.Quantity, Is.EqualTo(FixedPoint2.New(20)));
            Assert.That(bicaridine.Overdose, Is.True,
                "Blood is the only group that carries dose flags; 20u Bicaridine should OD.");
            Assert.That(bicaridine.Underdose, Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BicaridineThresholdsMatchYaml()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var entityManager = server.ResolveDependency<IEntityManager>();
        var system = entityManager.System<HealthAnalyzerReagentSystem>();

        await server.WaitAssertion(() =>
        {
            var thresholds = system.GetDoseThresholds(protoMan.Index(Bicaridine));
            Assert.That(thresholds.HarmfulMin, Is.Not.Null,
                "Bicaridine's metabolism gates harmful effects on a min threshold.");
            Assert.That(thresholds.HarmfulMin!.Value, Is.EqualTo(FixedPoint2.New(15)));
            Assert.That(thresholds.HarmfulMax, Is.Null, "Bicaridine has no self-gated max threshold.");
            Assert.That(thresholds.BeneficialMin, Is.Null, "Bicaridine has no self-gated beneficial effect.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BeneficialGatedReagentClassifiesAsBeneficial()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var entityManager = server.ResolveDependency<IEntityManager>();
        var system = entityManager.System<HealthAnalyzerReagentSystem>();

        await server.WaitAssertion(() =>
        {
            var thresholds = system.GetDoseThresholds(protoMan.Index<ReagentPrototype>(TestUnderdoseReagent));
            Assert.That(thresholds.BeneficialMin, Is.Not.Null,
                "Healing-gated reagents should surface a beneficial activation threshold.");
            Assert.That(thresholds.BeneficialMin!.Value, Is.EqualTo(FixedPoint2.New(8)));
            Assert.That(thresholds.HarmfulMin, Is.Null);
            Assert.That(thresholds.HarmfulMax, Is.Null);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PureFlavorReagentProducesNoThresholds()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var entityManager = server.ResolveDependency<IEntityManager>();
        var system = entityManager.System<HealthAnalyzerReagentSystem>();

        await server.WaitAssertion(() =>
        {
            var thresholds = system.GetDoseThresholds(protoMan.Index<ReagentPrototype>(TestNeutralFlavorReagent));
            Assert.That(thresholds.HarmfulMin, Is.Null, "Emote/PopupMessage effects must not generate harmful thresholds.");
            Assert.That(thresholds.HarmfulMax, Is.Null);
            Assert.That(thresholds.BeneficialMin, Is.Null);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RangeHarmfulReagentProducesBothBounds()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var entityManager = server.ResolveDependency<IEntityManager>();
        var system = entityManager.System<HealthAnalyzerReagentSystem>();

        await server.WaitAssertion(() =>
        {
            var thresholds = system.GetDoseThresholds(protoMan.Index<ReagentPrototype>(TestRangeHarmfulReagent));
            Assert.That(thresholds.HarmfulMax, Is.Not.Null, "max-gated slow effect should surface as HarmfulMax.");
            Assert.That(thresholds.HarmfulMax!.Value, Is.EqualTo(FixedPoint2.New(10)));
            Assert.That(thresholds.HarmfulMin, Is.Not.Null, "min-gated poison effect should surface as HarmfulMin.");
            Assert.That(thresholds.HarmfulMin!.Value, Is.EqualTo(FixedPoint2.New(20)));
            Assert.That(thresholds.BeneficialMin, Is.Null);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SelfDecayReagentProducesNoThresholds()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var entityManager = server.ResolveDependency<IEntityManager>();
        var system = entityManager.System<HealthAnalyzerReagentSystem>();

        await server.WaitAssertion(() =>
        {
            var thresholds = system.GetDoseThresholds(protoMan.Index<ReagentPrototype>(TestSelfDecayReagent));
            Assert.That(thresholds.HarmfulMin, Is.Null, "Self-decaying AdjustReagent (negative amount) must not be harmful.");
            Assert.That(thresholds.HarmfulMax, Is.Null);
            Assert.That(thresholds.BeneficialMin, Is.Null);
        });

        await pair.CleanReturnAsync();
    }
}
