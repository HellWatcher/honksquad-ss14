using System.Collections.Generic;
using Content.Shared.Preferences;
using Content.Shared.Traits;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.RussStation.Traits;

/// <summary>
/// Tag-based quirk exclusion and coexistence scenarios (PR #381) for
/// <see cref="HumanoidCharacterProfile"/>. Shares the <c>Prototypes</c> fixture
/// declared in <c>TraitPointBuyTest.cs</c>.
/// </summary>
public sealed partial class TraitPointBuyTest
{
    /// <summary>
    /// Adding a trait with excludedTags should remove any selected trait whose tag matches.
    /// </summary>
    [Test]
    public async Task TagExclusion_AddingTraitRemovesConflicting()
    {
        var server = Server;
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
    }

    /// <summary>
    /// Two traits that don't exclude each other's tags should coexist.
    /// </summary>
    [Test]
    public async Task TagExclusion_NonConflictingTraitsCoexist()
    {
        var server = Server;
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
    }

    /// <summary>
    /// Traits without tags should never conflict with anything.
    /// </summary>
    [Test]
    public async Task TagExclusion_TraitsWithoutTagNeverConflict()
    {
        var server = Server;
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
    }

    /// <summary>
    /// GetValidTraits should filter out traits that conflict with already-validated ones.
    /// </summary>
    [Test]
    public async Task GetValidTraits_FiltersConflictingTraits()
    {
        var server = Server;
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
    }

    /// <summary>
    /// Cross-set exclusion: an upstream trait and a fork quirk wired on the same tag domain
    /// must reject the second pick. Uses real prototypes (Muted from upstream disabilities,
    /// BoomingVoice from fork quirks). Muted excludes "speech", BoomingVoice carries it.
    /// </summary>
    [Test]
    public async Task TagExclusion_UpstreamMutedRejectsForkBoomingVoice()
    {
        var server = Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var profile = HumanoidCharacterProfile.DefaultWithSpecies();

            profile = profile.WithTraitPreference("Muted", protoMan);
            Assert.That(profile.TraitPreferences, Does.Contain(new ProtoId<TraitPrototype>("Muted")));

            profile = profile.WithTraitPreference("BoomingVoice", protoMan);
            Assert.That(profile.TraitPreferences, Does.Not.Contain(new ProtoId<TraitPrototype>("BoomingVoice")),
                "BoomingVoice should be rejected when Muted is already taken (shared speech tag).");
        });
    }

    /// <summary>
    /// Sight-domain cross-set: Blindness and ScarredEye both carry "sight" as a tag and an
    /// excludedTag, so the two cannot coexist. The newer pick wins (matches the synthetic
    /// TagExclusion_AddingTraitRemovesConflicting semantic).
    /// </summary>
    [Test]
    public async Task TagExclusion_BlindnessAndScarredEyeMutuallyExclusive()
    {
        var server = Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var profile = HumanoidCharacterProfile.DefaultWithSpecies();

            profile = profile.WithTraitPreference("Blindness", protoMan);
            Assert.That(profile.TraitPreferences, Does.Contain(new ProtoId<TraitPrototype>("Blindness")));

            profile = profile.WithTraitPreference("ScarredEye", protoMan);
            Assert.That(profile.TraitPreferences, Does.Contain(new ProtoId<TraitPrototype>("ScarredEye")),
                "Newer ScarredEye should be present.");
            Assert.That(profile.TraitPreferences, Does.Not.Contain(new ProtoId<TraitPrototype>("Blindness")),
                "Blindness should be removed by newer ScarredEye (shared sight tag).");
        });
    }

    /// <summary>
    /// Awareness cross-set: PainNumbness and SelfAware are functional opposites and both
    /// carry "awareness" as a tag and an excludedTag, so the two cannot coexist. The newer
    /// pick wins.
    /// </summary>
    [Test]
    public async Task TagExclusion_PainNumbnessAndSelfAwareMutuallyExclusive()
    {
        var server = Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var profile = HumanoidCharacterProfile.DefaultWithSpecies();

            profile = profile.WithTraitPreference("PainNumbness", protoMan);
            Assert.That(profile.TraitPreferences, Does.Contain(new ProtoId<TraitPrototype>("PainNumbness")));

            profile = profile.WithTraitPreference("SelfAware", protoMan);
            Assert.That(profile.TraitPreferences, Does.Contain(new ProtoId<TraitPrototype>("SelfAware")),
                "Newer SelfAware should be present.");
            Assert.That(profile.TraitPreferences, Does.Not.Contain(new ProtoId<TraitPrototype>("PainNumbness")),
                "PainNumbness should be removed by newer SelfAware (shared awareness tag).");
        });
    }

    /// <summary>
    /// Asymmetric mobility/sprint exclusion (forward): ImpairedMobility excludes "sprint",
    /// Skittish carries "sprint" as one of its tags. ImpairedMobility selected first.
    /// </summary>
    [Test]
    public async Task TagExclusion_ImpairedMobilityRejectsSkittish()
    {
        var server = Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var profile = HumanoidCharacterProfile.DefaultWithSpecies();

            profile = profile.WithTraitPreference("ImpairedMobility", protoMan);
            Assert.That(profile.TraitPreferences, Does.Contain(new ProtoId<TraitPrototype>("ImpairedMobility")));

            profile = profile.WithTraitPreference("Skittish", protoMan);
            Assert.That(profile.TraitPreferences, Does.Not.Contain(new ProtoId<TraitPrototype>("Skittish")),
                "Skittish should be rejected when ImpairedMobility is already taken (sprint excluded).");
        });
    }

    /// <summary>
    /// Asymmetric mobility/sprint exclusion (reverse): Skittish selected first, then ImpairedMobility.
    /// Bidirectional logic must still remove Skittish via ImpairedMobility's sprint exclude.
    /// </summary>
    [Test]
    public async Task TagExclusion_SkittishRejectsImpairedMobility()
    {
        var server = Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var profile = HumanoidCharacterProfile.DefaultWithSpecies();

            profile = profile.WithTraitPreference("Skittish", protoMan);
            Assert.That(profile.TraitPreferences, Does.Contain(new ProtoId<TraitPrototype>("Skittish")));

            profile = profile.WithTraitPreference("ImpairedMobility", protoMan);
            Assert.That(profile.TraitPreferences, Does.Contain(new ProtoId<TraitPrototype>("ImpairedMobility")),
                "ImpairedMobility should still be selectable.");
            Assert.That(profile.TraitPreferences, Does.Not.Contain(new ProtoId<TraitPrototype>("Skittish")),
                "Skittish should be removed when ImpairedMobility is added (sprint tag excluded).");
        });
    }

    /// <summary>
    /// Coexistence sanity: SoftSpoken and an accent both carry the "speech" tag but neither excludes it.
    /// They should coexist — speaking softly with a French accent is fine.
    /// </summary>
    [Test]
    public async Task TagCoexistence_SoftSpokenAndAccent()
    {
        var server = Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var profile = HumanoidCharacterProfile.DefaultWithSpecies();

            profile = profile.WithTraitPreference("SoftSpoken", protoMan);
            profile = profile.WithTraitPreference("FrenchAccent", protoMan);

            Assert.That(profile.TraitPreferences, Does.Contain(new ProtoId<TraitPrototype>("SoftSpoken")));
            Assert.That(profile.TraitPreferences, Does.Contain(new ProtoId<TraitPrototype>("FrenchAccent")),
                "SoftSpoken and an accent share the speech tag but neither excludes it; they should coexist.");
        });
    }

    /// <summary>
    /// Coexistence sanity: unrelated domains shouldn't collide. Muted (speech) and Tough (wounds)
    /// share no tags, so both should sit in the profile together.
    /// </summary>
    [Test]
    public async Task TagCoexistence_MutedAndTough()
    {
        var server = Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var profile = HumanoidCharacterProfile.DefaultWithSpecies();

            profile = profile.WithTraitPreference("Muted", protoMan);
            profile = profile.WithTraitPreference("Tough", protoMan);

            Assert.That(profile.TraitPreferences, Does.Contain(new ProtoId<TraitPrototype>("Muted")));
            Assert.That(profile.TraitPreferences, Does.Contain(new ProtoId<TraitPrototype>("Tough")),
                "Muted (speech domain) and Tough (wounds domain) share no tags and should coexist.");
        });
    }
}
