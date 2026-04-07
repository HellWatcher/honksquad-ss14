using System.Linq;
using Content.Server.RussStation.Medical;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.DoAfter;
using Content.Shared.RussStation.Medical;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.RussStation.Medical;

[TestFixture]
[TestOf(typeof(AutosurgeonSystem))]
public sealed class AutosurgeonTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: AutosurgeonTestBody
  components:
  - type: Body
  - type: MobState
    allowedStates:
    - Alive
    - Critical
    - Dead
  - type: MobThresholds
    thresholds:
      0: Alive
      100: Critical
      200: Dead
  - type: Damageable
  - type: Bloodstream
    bloodlossDamage:
      types:
        Bloodloss: 0.5
    bloodlossHealDamage:
      types:
        Bloodloss: -1
  - type: SolutionContainerManager

- type: entity
  id: AutosurgeonTestOrgan
  components:
  - type: Organ
    category: Heart

- type: entity
  id: AutosurgeonTestOrganLungs
  components:
  - type: Organ
    category: Lungs

- type: entity
  id: AutosurgeonTestExistingHeart
  components:
  - type: Organ
    category: Heart

- type: entity
  id: AutosurgeonTestBodyWithHeart
  components:
  - type: Body
  - type: MobState
    allowedStates:
    - Alive
    - Critical
    - Dead
  - type: MobThresholds
    thresholds:
      0: Alive
      100: Critical
      200: Dead
  - type: Damageable
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
        - id: AutosurgeonTestExistingHeart

- type: entity
  id: AutosurgeonTestUser
  components:
  - type: DoAfter

- type: entity
  id: AutosurgeonTestDevice
  components:
  - type: Autosurgeon
    organPrototype: AutosurgeonTestOrgan
";

    [Test]
    public async Task InstallOrganIntoEmptyBodyTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var timing = server.ResolveDependency<IGameTiming>();
        var mapData = await pair.CreateTestMap();

        EntityUid body = default;

        await server.WaitAssertion(() =>
        {
            var doAfterSys = entMan.System<SharedDoAfterSystem>();
            var containerSys = entMan.System<SharedContainerSystem>();

            body = entMan.SpawnEntity("AutosurgeonTestBody", mapData.GridCoords);
            var user = entMan.SpawnEntity("AutosurgeonTestUser", mapData.GridCoords);
            var autosurgeon = entMan.SpawnEntity("AutosurgeonTestDevice", mapData.GridCoords);

            var bodyComp = entMan.GetComponent<BodyComponent>(body);
            Assert.That(bodyComp.Organs, Is.Not.Null, "Body should have organs container");

            var organCountBefore = bodyComp.Organs!.ContainedEntities.Count;

            // Start a DoAfter with minimal delay to trigger the install
            var doAfterArgs = new DoAfterArgs(entMan, user, timing.TickPeriod / 2,
                new AutosurgeonDoAfterEvent(), autosurgeon, target: body, used: autosurgeon)
            {
                RequireCanInteract = false,
                NeedHand = false,
                BreakOnMove = false,
            };
            var started = doAfterSys.TryStartDoAfter(doAfterArgs);
            Assert.That(started, Is.True, "DoAfter should start successfully");
        });

        // Wait for DoAfter to complete
        await server.WaitRunTicks(3);

        await server.WaitAssertion(() =>
        {
            var bodyComp = entMan.GetComponent<BodyComponent>(body);
            Assert.That(bodyComp.Organs, Is.Not.Null);

            // Should have the new organ installed
            var hasHeart = false;
            foreach (var organ in bodyComp.Organs!.ContainedEntities)
            {
                if (entMan.TryGetComponent<OrganComponent>(organ, out var organComp) &&
                    organComp.Category?.Id == "Heart")
                {
                    hasHeart = true;
                    break;
                }
            }

            Assert.That(hasHeart, Is.True,
                "Body should have a Heart organ after autosurgeon install");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ReplacesExistingOrganOfSameCategoryTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var timing = server.ResolveDependency<IGameTiming>();
        var mapData = await pair.CreateTestMap();

        EntityUid body = default;
        EntityUid existingOrgan = default;

        await server.WaitAssertion(() =>
        {
            var doAfterSys = entMan.System<SharedDoAfterSystem>();

            body = entMan.SpawnEntity("AutosurgeonTestBodyWithHeart", mapData.GridCoords);
            var user = entMan.SpawnEntity("AutosurgeonTestUser", mapData.GridCoords);
            var autosurgeon = entMan.SpawnEntity("AutosurgeonTestDevice", mapData.GridCoords);

            var bodyComp = entMan.GetComponent<BodyComponent>(body);
            Assert.That(bodyComp.Organs, Is.Not.Null);

            // Find the existing heart
            foreach (var organ in bodyComp.Organs!.ContainedEntities)
            {
                if (entMan.TryGetComponent<OrganComponent>(organ, out var organComp) &&
                    organComp.Category?.Id == "Heart")
                {
                    existingOrgan = organ;
                    break;
                }
            }

            Assert.That(existingOrgan, Is.Not.EqualTo(default(EntityUid)),
                "Body should have an existing heart organ");

            var doAfterArgs = new DoAfterArgs(entMan, user, timing.TickPeriod / 2,
                new AutosurgeonDoAfterEvent(), autosurgeon, target: body, used: autosurgeon)
            {
                RequireCanInteract = false,
                NeedHand = false,
                BreakOnMove = false,
            };
            doAfterSys.TryStartDoAfter(doAfterArgs);
        });

        await server.WaitRunTicks(3);

        await server.WaitAssertion(() =>
        {
            var containerSys = entMan.System<SharedContainerSystem>();
            var bodyComp = entMan.GetComponent<BodyComponent>(body);
            Assert.That(bodyComp.Organs, Is.Not.Null);

            // The existing organ should have been removed from the body
            Assert.That(bodyComp.Organs!.ContainedEntities.Contains(existingOrgan), Is.False,
                "Old heart should be removed from body container");

            // Should still have exactly one heart (the new one)
            var heartCount = 0;
            foreach (var organ in bodyComp.Organs.ContainedEntities)
            {
                if (entMan.TryGetComponent<OrganComponent>(organ, out var organComp) &&
                    organComp.Category?.Id == "Heart")
                {
                    heartCount++;
                }
            }

            Assert.That(heartCount, Is.EqualTo(1),
                "Body should have exactly one heart after replacement");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AutosurgeonDeletedAfterUseTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var timing = server.ResolveDependency<IGameTiming>();
        var mapData = await pair.CreateTestMap();

        EntityUid autosurgeon = default;

        await server.WaitAssertion(() =>
        {
            var doAfterSys = entMan.System<SharedDoAfterSystem>();

            var body = entMan.SpawnEntity("AutosurgeonTestBody", mapData.GridCoords);
            var user = entMan.SpawnEntity("AutosurgeonTestUser", mapData.GridCoords);
            autosurgeon = entMan.SpawnEntity("AutosurgeonTestDevice", mapData.GridCoords);

            var doAfterArgs = new DoAfterArgs(entMan, user, timing.TickPeriod / 2,
                new AutosurgeonDoAfterEvent(), autosurgeon, target: body, used: autosurgeon)
            {
                RequireCanInteract = false,
                NeedHand = false,
                BreakOnMove = false,
            };
            doAfterSys.TryStartDoAfter(doAfterArgs);
        });

        // Wait for DoAfter + QueueDel
        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.Deleted(autosurgeon), Is.True,
                "Autosurgeon should be deleted after single use");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NoInstallWithoutBodyComponentTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var timing = server.ResolveDependency<IGameTiming>();
        var mapData = await pair.CreateTestMap();

        EntityUid autosurgeon = default;

        await server.WaitAssertion(() =>
        {
            var doAfterSys = entMan.System<SharedDoAfterSystem>();

            // Target entity without BodyComponent
            var target = entMan.SpawnEntity("AutosurgeonTestUser", mapData.GridCoords);
            var user = entMan.SpawnEntity("AutosurgeonTestUser", mapData.GridCoords);
            autosurgeon = entMan.SpawnEntity("AutosurgeonTestDevice", mapData.GridCoords);

            var doAfterArgs = new DoAfterArgs(entMan, user, timing.TickPeriod / 2,
                new AutosurgeonDoAfterEvent(), autosurgeon, target: target, used: autosurgeon)
            {
                RequireCanInteract = false,
                NeedHand = false,
                BreakOnMove = false,
            };
            doAfterSys.TryStartDoAfter(doAfterArgs);
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            // Autosurgeon should NOT be deleted since the install failed
            Assert.That(entMan.Deleted(autosurgeon), Is.False,
                "Autosurgeon should not be consumed when target has no body");
        });

        await pair.CleanReturnAsync();
    }
}
