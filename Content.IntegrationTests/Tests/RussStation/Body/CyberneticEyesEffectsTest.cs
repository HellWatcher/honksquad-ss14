using System.Linq;
using Content.Server.RussStation.Body;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Flash;
using Content.Shared.RussStation.Body;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.RussStation.Body;

[TestFixture]
[TestOf(typeof(CyberneticOrganEffectsSystem))]
public sealed class CyberneticEyesEffectsTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: CyberEyesTestBody
  components:
  - type: Body

- type: entity
  id: CyberEyesTestEyes
  components:
  - type: Organ
    category: Eyes
  - type: CyberneticEyes

- type: entity
  id: CyberEyesTestBodyWithEyes
  components:
  - type: Body
  - type: Bloodstream
    bloodlossDamage:
      types:
        Bloodloss: 0.5
    bloodlossHealDamage:
      types:
        Bloodloss: -1
  - type: SolutionContainerManager
  - type: EntityTableContainerFill
    containers:
      body_organs: !type:AllSelector
        children:
        - id: CyberEyesTestEyes
";

    [Test]
    public async Task EyesCancelFlashTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entMan.SpawnEntity("CyberEyesTestBodyWithEyes", mapData.GridCoords);

            var ev = new FlashAttemptEvent(body, null, null);
            entMan.EventBus.RaiseLocalEvent(body, ref ev);

            Assert.That(ev.Cancelled, Is.True,
                "Flash attempt should be cancelled with cybernetic eyes");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EyesNoProtectionWithoutOrganTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entMan.SpawnEntity("CyberEyesTestBody", mapData.GridCoords);

            var ev = new FlashAttemptEvent(body, null, null);
            entMan.EventBus.RaiseLocalEvent(body, ref ev);

            Assert.That(ev.Cancelled, Is.False,
                "Flash should not be cancelled without cybernetic eyes");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EyesDynamicInsertionProvidesFlashProtectionTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entMan.SpawnEntity("CyberEyesTestBody", mapData.GridCoords);

            // Flash should work without eyes
            var flashBefore = new FlashAttemptEvent(body, null, null);
            entMan.EventBus.RaiseLocalEvent(body, ref flashBefore);
            Assert.That(flashBefore.Cancelled, Is.False,
                "Precondition: flash should not be cancelled without cybernetic eyes");

            // Dynamically insert cybernetic eyes
            entMan.SpawnInContainerOrDrop("CyberEyesTestEyes", body, BodyComponent.ContainerID);

            var flashAfter = new FlashAttemptEvent(body, null, null);
            entMan.EventBus.RaiseLocalEvent(body, ref flashAfter);
            Assert.That(flashAfter.Cancelled, Is.True,
                "Dynamically inserted cybernetic eyes should provide flash protection");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EyesRemovalRestoresFlashVulnerabilityTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var containerSys = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<SharedContainerSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entMan.SpawnEntity("CyberEyesTestBodyWithEyes", mapData.GridCoords);

            // Verify flash is blocked with eyes
            var flashBefore = new FlashAttemptEvent(body, null, null);
            entMan.EventBus.RaiseLocalEvent(body, ref flashBefore);
            Assert.That(flashBefore.Cancelled, Is.True, "Precondition: flash should be cancelled with eyes");

            // Remove the eyes organ
            var organContainer = containerSys.GetContainer(body, BodyComponent.ContainerID);
            foreach (var ent in organContainer.ContainedEntities.ToList())
            {
                if (entMan.HasComponent<CyberneticEyesComponent>(ent))
                    containerSys.Remove(ent, organContainer);
            }

            var flashAfter = new FlashAttemptEvent(body, null, null);
            entMan.EventBus.RaiseLocalEvent(body, ref flashAfter);
            Assert.That(flashAfter.Cancelled, Is.False,
                "Flash should not be cancelled after removing cybernetic eyes");
        });

        await pair.CleanReturnAsync();
    }
}
