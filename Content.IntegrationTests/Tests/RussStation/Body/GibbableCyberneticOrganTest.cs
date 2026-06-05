using System.Collections.Generic;
using Content.Shared.Body;
using Content.Shared.Gibbing;
using Content.Shared.RussStation.Body;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.RussStation.Body;

/// <summary>
/// Verifies the fork's change to <see cref="GibbableOrganSystem"/>: a plain gibbable organ is
/// added to the giblet set when its body is gibbed, but an organ that is also a
/// <see cref="CyberneticOrganComponent"/> is excluded so cybernetics don't turn into gibs.
/// </summary>
[TestFixture]
[TestOf(typeof(GibbableOrganSystem))]
public sealed class GibbableCyberneticOrganTest
{
    [Test]
    public async Task OrganicOrganIsGibbedButCyberneticIsNotTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var body = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var bodyComp = entMan.EnsureComponent<BodyComponent>(body);

            var organicOrgan = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            entMan.AddComponent<GibbableOrganComponent>(organicOrgan);

            var cyberneticOrgan = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            entMan.AddComponent<GibbableOrganComponent>(cyberneticOrgan);
            entMan.AddComponent<CyberneticOrganComponent>(cyberneticOrgan);

            // Organic organ: should be collected into the giblet set.
            var organicGiblets = new HashSet<EntityUid>();
            var organicEv = new BodyRelayedEvent<BeingGibbedEvent>((body, bodyComp), new BeingGibbedEvent(organicGiblets));
            entMan.EventBus.RaiseLocalEvent(organicOrgan, ref organicEv);
            Assert.That(organicGiblets, Does.Contain(organicOrgan),
                "A plain gibbable organ should be added to the giblets.");

            // Cybernetic organ: should be skipped.
            var cyberneticGiblets = new HashSet<EntityUid>();
            var cyberneticEv = new BodyRelayedEvent<BeingGibbedEvent>((body, bodyComp), new BeingGibbedEvent(cyberneticGiblets));
            entMan.EventBus.RaiseLocalEvent(cyberneticOrgan, ref cyberneticEv);
            Assert.That(cyberneticGiblets, Does.Not.Contain(cyberneticOrgan),
                "A cybernetic organ should be excluded from the giblets.");

            entMan.DeleteEntity(body);
            entMan.DeleteEntity(organicOrgan);
            entMan.DeleteEntity(cyberneticOrgan);
        });

        await pair.CleanReturnAsync();
    }
}
