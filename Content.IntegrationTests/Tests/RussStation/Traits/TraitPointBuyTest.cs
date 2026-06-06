using System.Collections.Generic;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Preferences;
using Content.Shared.Traits;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.RussStation.Traits;

/// <summary>
/// Tests for the global trait point budget system (PR #376)
/// and tag-based quirk exclusion system (PR #381).
/// </summary>
/// <remarks>
/// The point budget / category cap scenarios live here; the tag exclusion and
/// coexistence scenarios live in <c>TraitPointBuyTest.TagExclusion.cs</c>.
/// Both partials share the <see cref="Prototypes"/> fixture below.
/// </remarks>
[TestOf(typeof(HumanoidCharacterProfile))]
public sealed partial class TraitPointBuyTest : GameTest
{
    // Test trait prototypes with tags and costs for integration testing.
    // MaxTraitPoints CVar defaults to 10.
    [TestPrototypes]
    private const string Prototypes = @"
- type: traitCategory
  id: TestCombat
  name: generic-unknown
  maxTraitPoints: 5

- type: trait
  id: TestTraitA
  name: generic-unknown
  cost: 3
  category: TestCombat
  tags:
  - test_a
  excludedTags:
  - test_b

- type: trait
  id: TestTraitB
  name: generic-unknown
  cost: 3
  category: TestCombat
  tags:
  - test_b
  excludedTags:
  - test_a

- type: trait
  id: TestTraitC
  name: generic-unknown
  cost: 2
  category: TestCombat
  tags:
  - test_c
  excludedTags: []

- type: trait
  id: TestTraitExpensive
  name: generic-unknown
  cost: 8

- type: trait
  id: TestTraitCheap
  name: generic-unknown
  cost: 1

- type: trait
  id: TestTraitNegative
  name: generic-unknown
  cost: -3

- type: trait
  id: TestTraitNoTag
  name: generic-unknown
  cost: 2
";

    /// <summary>
    /// Traits exceeding the global point budget (default 10) should be rejected.
    /// </summary>
    [Test]
    public async Task GlobalBudget_RejectsOverBudgetTrait()
    {
        var server = Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var profile = HumanoidCharacterProfile.DefaultWithSpecies();

            // Add expensive trait (cost 8) - fits within 10 budget
            profile = profile.WithTraitPreference("TestTraitExpensive", protoMan);
            Assert.That(profile.TraitPreferences, Does.Contain(new ProtoId<TraitPrototype>("TestTraitExpensive")));

            // Add another trait (cost 3) - total would be 11, over budget
            profile = profile.WithTraitPreference("TestTraitA", protoMan);
            Assert.That(profile.TraitPreferences, Does.Not.Contain(new ProtoId<TraitPrototype>("TestTraitA")),
                "Trait should be rejected when it would exceed global budget.");
        });
    }

    /// <summary>
    /// Negative-cost traits refund points and should not be blocked by the budget.
    /// </summary>
    [Test]
    public async Task GlobalBudget_NegativeCostTraitsAlwaysAllowed()
    {
        var server = Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var profile = HumanoidCharacterProfile.DefaultWithSpecies();

            // Fill budget with expensive trait (cost 8)
            profile = profile.WithTraitPreference("TestTraitExpensive", protoMan);

            // Negative cost trait (-3) should always be allowed
            profile = profile.WithTraitPreference("TestTraitNegative", protoMan);
            Assert.That(profile.TraitPreferences, Does.Contain(new ProtoId<TraitPrototype>("TestTraitNegative")),
                "Negative cost traits should always be allowed regardless of budget.");

            // Now total is 5 (8 - 3), so a cost-3 trait should fit
            profile = profile.WithTraitPreference("TestTraitCheap", protoMan);
            Assert.That(profile.TraitPreferences, Does.Contain(new ProtoId<TraitPrototype>("TestTraitCheap")),
                "After negative-cost trait, budget should have room for more.");
        });
    }

    /// <summary>
    /// Category caps should reject traits that exceed the per-category limit.
    /// </summary>
    [Test]
    public async Task CategoryCap_RejectsOverCategoryLimit()
    {
        var server = Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var profile = HumanoidCharacterProfile.DefaultWithSpecies();

            // TestCombat category has maxTraitPoints: 5
            // Add trait A (cost 3) - fits
            profile = profile.WithTraitPreference("TestTraitA", protoMan);
            Assert.That(profile.TraitPreferences, Does.Contain(new ProtoId<TraitPrototype>("TestTraitA")));

            // Add trait C (cost 2, same category) - total 5, fits exactly
            profile = profile.WithTraitPreference("TestTraitC", protoMan);
            Assert.That(profile.TraitPreferences, Does.Contain(new ProtoId<TraitPrototype>("TestTraitC")),
                "Traits at exactly the category cap should be allowed.");
        });
    }

    /// <summary>
    /// GetValidTraits should respect both global budget and tag exclusions together.
    /// </summary>
    [Test]
    public async Task GetValidTraits_RespectsGlobalBudget()
    {
        var server = Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var profile = HumanoidCharacterProfile.DefaultWithSpecies();

            // Two expensive traits that would bust the budget (8 + 3 = 11 > 10)
            var traits = new List<ProtoId<TraitPrototype>> { "TestTraitExpensive", "TestTraitA" };
            var valid = profile.GetValidTraits(traits, protoMan);

            Assert.That(valid, Does.Contain(new ProtoId<TraitPrototype>("TestTraitExpensive")),
                "First expensive trait should fit in budget.");
            Assert.That(valid, Does.Not.Contain(new ProtoId<TraitPrototype>("TestTraitA")),
                "Second trait should be filtered when it exceeds global budget.");
        });
    }
}
