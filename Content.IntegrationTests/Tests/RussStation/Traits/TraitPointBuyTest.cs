using System.Collections.Generic;
using Content.Shared.Preferences;
using Content.Shared.Traits;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.RussStation.Traits;

/// <summary>
/// Tests for the global trait point budget system (PR #376)
/// and tag-based quirk exclusion system (PR #381).
/// </summary>
[TestFixture]
[TestOf(typeof(HumanoidCharacterProfile))]
public sealed class TraitPointBuyTest
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
    /// Adding a trait with excludedTags should remove any selected trait whose tag matches.
    /// </summary>
    [Test]
    public async Task TagExclusion_AddingTraitRemovesConflicting()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var profile = HumanoidCharacterProfile.DefaultWithSpecies();

            // Add trait A (tag: test_a, excludes: test_b)
            profile = profile.WithTraitPreference("TestTraitA", protoMan);
            Assert.That(profile.TraitPreferences, Does.Contain(new ProtoId<TraitPrototype>("TestTraitA")));

            // Add trait B (tag: test_b, excludes: test_a) - should remove A
            profile = profile.WithTraitPreference("TestTraitB", protoMan);
            Assert.That(profile.TraitPreferences, Does.Contain(new ProtoId<TraitPrototype>("TestTraitB")),
                "Newly added trait should be present.");
            Assert.That(profile.TraitPreferences, Does.Not.Contain(new ProtoId<TraitPrototype>("TestTraitA")),
                "Conflicting trait should be removed when excluded by new trait.");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Two traits that don't exclude each other's tags should coexist.
    /// </summary>
    [Test]
    public async Task TagExclusion_NonConflictingTraitsCoexist()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var profile = HumanoidCharacterProfile.DefaultWithSpecies();

            profile = profile.WithTraitPreference("TestTraitA", protoMan);
            profile = profile.WithTraitPreference("TestTraitC", protoMan);

            Assert.That(profile.TraitPreferences, Does.Contain(new ProtoId<TraitPrototype>("TestTraitA")));
            Assert.That(profile.TraitPreferences, Does.Contain(new ProtoId<TraitPrototype>("TestTraitC")),
                "Non-conflicting traits should both be present.");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Traits without tags should never conflict with anything.
    /// </summary>
    [Test]
    public async Task TagExclusion_TraitsWithoutTagNeverConflict()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var profile = HumanoidCharacterProfile.DefaultWithSpecies();

            profile = profile.WithTraitPreference("TestTraitA", protoMan);
            profile = profile.WithTraitPreference("TestTraitNoTag", protoMan);

            Assert.That(profile.TraitPreferences, Does.Contain(new ProtoId<TraitPrototype>("TestTraitA")));
            Assert.That(profile.TraitPreferences, Does.Contain(new ProtoId<TraitPrototype>("TestTraitNoTag")),
                "Trait without a tag should never be excluded.");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// GetValidTraits should filter out traits that conflict with already-validated ones.
    /// </summary>
    [Test]
    public async Task GetValidTraits_FiltersConflictingTraits()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var profile = HumanoidCharacterProfile.DefaultWithSpecies();

            // Pass both conflicting traits; first one wins, second gets filtered
            var traits = new List<ProtoId<TraitPrototype>> { "TestTraitA", "TestTraitB" };
            var valid = profile.GetValidTraits(traits, protoMan);

            Assert.That(valid, Does.Contain(new ProtoId<TraitPrototype>("TestTraitA")),
                "First trait should be kept.");
            Assert.That(valid, Does.Not.Contain(new ProtoId<TraitPrototype>("TestTraitB")),
                "Second conflicting trait should be filtered out.");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Traits exceeding the global point budget (default 10) should be rejected.
    /// </summary>
    [Test]
    public async Task GlobalBudget_RejectsOverBudgetTrait()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
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

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Negative-cost traits refund points and should not be blocked by the budget.
    /// </summary>
    [Test]
    public async Task GlobalBudget_NegativeCostTraitsAlwaysAllowed()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
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

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Category caps should reject traits that exceed the per-category limit.
    /// </summary>
    [Test]
    public async Task CategoryCap_RejectsOverCategoryLimit()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
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

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// GetValidTraits should respect both global budget and tag exclusions together.
    /// </summary>
    [Test]
    public async Task GetValidTraits_RespectsGlobalBudget()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
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

        await pair.CleanReturnAsync();
    }
}
