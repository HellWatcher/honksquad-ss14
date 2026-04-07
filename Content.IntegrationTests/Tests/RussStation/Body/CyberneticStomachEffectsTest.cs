using System.Linq;
using Content.Server.RussStation.Body;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.RussStation.Body;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.RussStation.Body;

[TestFixture]
[TestOf(typeof(CyberneticOrganEffectsSystem))]
public sealed class CyberneticStomachEffectsTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: CyberStomachTestStomach
  components:
  - type: Organ
    category: Stomach
  - type: CyberneticStomach

- type: entity
  id: CyberStomachTestBodyNoHunger
  components:
  - type: Body

- type: entity
  id: CyberStomachTestBodyWithStomach
  components:
  - type: Body
  - type: Hunger
  - type: EntityTableContainerFill
    containers:
      body_organs: !type:AllSelector
        children:
        - id: CyberStomachTestStomach
";

    [Test]
    public async Task StomachReducesHungerDecayTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entMan.SpawnEntity("CyberStomachTestBodyWithStomach", mapData.GridCoords);
            var hunger = entMan.GetComponent<HungerComponent>(body);

            // CyberneticStomachComponent.DecayMultiplier is 0.5
            var defaultRate = 0.01666666666f;
            var expected = defaultRate * 0.5f;
            Assert.That(hunger.BaseDecayRate, Is.EqualTo(expected).Within(0.0001f),
                "Hunger decay should be halved with cybernetic stomach");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StomachRestoresDecayOnRemovalTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var containerSys = entMan.System<SharedContainerSystem>();
            var body = entMan.SpawnEntity("CyberStomachTestBodyWithStomach", mapData.GridCoords);
            var hunger = entMan.GetComponent<HungerComponent>(body);

            var reducedRate = hunger.BaseDecayRate;
            var defaultRate = 0.01666666666f;
            Assert.That(reducedRate, Is.LessThan(defaultRate),
                "Rate should be reduced with stomach");

            // Remove stomach
            var organContainer = containerSys.GetContainer(body, BodyComponent.ContainerID);
            foreach (var ent in organContainer.ContainedEntities.ToList())
            {
                if (entMan.HasComponent<CyberneticStomachComponent>(ent))
                    containerSys.Remove(ent, organContainer);
            }

            Assert.That(hunger.BaseDecayRate, Is.EqualTo(defaultRate).Within(0.0001f),
                "Hunger decay should be restored to default after stomach removal");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StomachDecayRateRestoredAfterRemoveAndReinsertTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var containerSys = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<SharedContainerSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entMan.SpawnEntity("CyberStomachTestBodyWithStomach", mapData.GridCoords);
            var hunger = entMan.GetComponent<HungerComponent>(body);
            var reducedRate = hunger.BaseDecayRate;

            // Remove the stomach organ
            var organContainer = containerSys.GetContainer(body, BodyComponent.ContainerID);
            EntityUid stomachOrgan = default;
            foreach (var ent in organContainer.ContainedEntities.ToList())
            {
                if (entMan.HasComponent<CyberneticStomachComponent>(ent))
                {
                    stomachOrgan = ent;
                    containerSys.Remove(ent, organContainer);
                    break;
                }
            }

            var restoredRate = hunger.BaseDecayRate;
            Assert.That(restoredRate, Is.GreaterThan(reducedRate),
                "Removing stomach should restore the original higher decay rate");

            // Re-insert the same stomach organ
            containerSys.Insert(stomachOrgan, organContainer);

            Assert.That(hunger.BaseDecayRate, Is.EqualTo(reducedRate).Within(0.0001f),
                "Re-inserting stomach should re-apply the reduced decay rate");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StomachNoEffectWithoutHungerComponentTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entMan.SpawnEntity("CyberStomachTestBodyNoHunger", mapData.GridCoords);

            Assert.That(entMan.HasComponent<HungerComponent>(body), Is.False,
                "Precondition: body should not have HungerComponent");

            // Insert stomach into body without Hunger - should not throw
            entMan.SpawnInContainerOrDrop("CyberStomachTestStomach", body, BodyComponent.ContainerID);

            Assert.That(entMan.HasComponent<HungerComponent>(body), Is.False,
                "Stomach insertion should not add HungerComponent to body that lacks one");
        });

        await pair.CleanReturnAsync();
    }
}
