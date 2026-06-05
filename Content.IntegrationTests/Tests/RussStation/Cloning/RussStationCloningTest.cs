using Content.Server.RussStation.Cloning;
using Content.Shared.Cloning;
using Content.Shared.Cloning.Events;
using Content.Shared.Humanoid;
using Content.Shared.RussStation.Traits;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.RussStation.Cloning;

/// <summary>
/// Verifies <see cref="RussStationCloningSystem"/> applies the fork's "copy a trait component
/// iff the original has it" rule on <see cref="CloningEvent"/>, driven by the
/// <c>RussStationBodyExtensions</c> cloning-settings prototype. Papyrophobia is one of the
/// listed trait components.
/// </summary>
[TestFixture]
[TestOf(typeof(RussStationCloningSystem))]
public sealed class RussStationCloningTest
{
    [Test]
    public async Task TraitCopiedToCloneWhenOriginalHasItTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var settings = protoMan.Index<CloningSettingsPrototype>(RussStationCloningSystem.ForkExtensionsId);

            var original = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            entMan.AddComponent<HumanoidProfileComponent>(original);
            entMan.AddComponent<PapyrophobiaComponent>(original);

            var clone = entMan.SpawnEntity(null, MapCoordinates.Nullspace);

            var ev = new CloningEvent(settings, clone);
            entMan.EventBus.RaiseLocalEvent(original, ref ev);

            Assert.That(entMan.HasComponent<PapyrophobiaComponent>(clone), Is.True,
                "A fork trait on the original should be copied onto the clone.");

            entMan.DeleteEntity(original);
            entMan.DeleteEntity(clone);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TraitStrippedFromCloneWhenOriginalLacksItTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var settings = protoMan.Index<CloningSettingsPrototype>(RussStationCloningSystem.ForkExtensionsId);

            // Original does NOT have the trait.
            var original = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            entMan.AddComponent<HumanoidProfileComponent>(original);

            // Clone spuriously has it; the copy-iff rule should remove it.
            var clone = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            entMan.AddComponent<PapyrophobiaComponent>(clone);

            var ev = new CloningEvent(settings, clone);
            entMan.EventBus.RaiseLocalEvent(original, ref ev);

            Assert.That(entMan.HasComponent<PapyrophobiaComponent>(clone), Is.False,
                "A trait the original lacks should be removed from the clone.");

            entMan.DeleteEntity(original);
            entMan.DeleteEntity(clone);
        });

        await pair.CleanReturnAsync();
    }
}
