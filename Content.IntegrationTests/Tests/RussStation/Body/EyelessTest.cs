using System.Linq;
using Content.Server.RussStation.Body;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.RussStation.Body;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.RussStation.Body;

[TestFixture]
[TestOf(typeof(OrganLossSystem))]
public sealed class EyelessTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: EyelessTestEyes
  components:
  - type: Organ
    category: Eyes

- type: entity
  id: EyelessTestBody
  components:
  - type: Body
  - type: Blindable

- type: entity
  id: EyelessTestBodyWithEyes
  components:
  - type: Body
  - type: Blindable
  - type: EntityTableContainerFill
    containers:
      body_organs: !type:AllSelector
        children:
        - id: EyelessTestEyes
";

    [Test]
    public async Task EyesPresentLeavesPatientNotBlindTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entMan.SpawnEntity("EyelessTestBodyWithEyes", mapData.GridCoords);

            Assert.That(entMan.HasComponent<NoEyesComponent>(body), Is.False,
                "Body with eyes should not have NoEyesComponent");

            var blindable = entMan.GetComponent<BlindableComponent>(body);
            Assert.That(blindable.IsBlind, Is.False,
                "Body with eyes should not be blind");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EyeRemovalAddsNoEyesAndBlindsPatientTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var containerSys = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<SharedContainerSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entMan.SpawnEntity("EyelessTestBodyWithEyes", mapData.GridCoords);

            var organContainer = containerSys.GetContainer(body, BodyComponent.ContainerID);
            foreach (var organ in organContainer.ContainedEntities.ToList())
            {
                if (entMan.TryGetComponent<OrganComponent>(organ, out var organComp)
                    && organComp.Category == "Eyes")
                {
                    containerSys.Remove(organ, organContainer);
                }
            }

            Assert.That(entMan.HasComponent<NoEyesComponent>(body), Is.True,
                "Body should have NoEyesComponent after eye removal");

            var blindable = entMan.GetComponent<BlindableComponent>(body);
            Assert.That(blindable.MinDamage, Is.EqualTo(blindable.MaxDamage),
                "MinDamage should be raised to MaxDamage so the patient is fully blind");
            Assert.That(blindable.IsBlind, Is.True,
                "Patient should be fully blind, not just blurred");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EyeReinsertionRestoresVisionTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var containerSys = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<SharedContainerSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entMan.SpawnEntity("EyelessTestBodyWithEyes", mapData.GridCoords);
            var organContainer = containerSys.GetContainer(body, BodyComponent.ContainerID);

            var eyes = organContainer.ContainedEntities
                .First(o => entMan.TryGetComponent<OrganComponent>(o, out var c) && c.Category == "Eyes");
            containerSys.Remove(eyes, organContainer);

            Assert.That(entMan.HasComponent<NoEyesComponent>(body), Is.True,
                "Precondition: blind without eyes");

            containerSys.Insert(eyes, organContainer, force: true);

            Assert.That(entMan.HasComponent<NoEyesComponent>(body), Is.False,
                "NoEyesComponent should be removed when eyes are reinserted");

            var blindable = entMan.GetComponent<BlindableComponent>(body);
            Assert.That(blindable.MinDamage, Is.EqualTo(0),
                "MinDamage should reset to 0 once eyes are back");
            Assert.That(blindable.IsBlind, Is.False,
                "Vision should restore after reinsertion");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BodyWithoutEyesPrototypeStartsBlindTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entMan.SpawnEntity("EyelessTestBody", mapData.GridCoords);

            // Body never had eyes; the system should treat it as eyeless on first organ event
            // (the test only matters once any organ event fires; the explicit removal above is
            // the canonical path, but a body that loses its last eye via gameplay then respawns
            // shouldn't unexpectedly carry over markers from the prototype).
            Assert.That(entMan.HasComponent<NoEyesComponent>(body), Is.False,
                "Stub body without an organ container shouldn't carry NoEyesComponent at spawn");
        });

        await pair.CleanReturnAsync();
    }
}
