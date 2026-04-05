using System.Linq;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server.CartridgeLoader;
using Content.Shared.CartridgeLoader;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.RussStation.CartridgeLoader;

public sealed class ForkCartridgeInstallerTest : InteractionTest
{
    private const string TestCartridgeA = "ForkTestCartridgeA";
    private const string TestCartridgeB = "ForkTestCartridgeB";
    private const string TestCartridgeC = "ForkTestCartridgeC";
    private const string FilterComp = "Physics";

    [TestPrototypes]
    private const string TestPrototypes = @"
- type: entity
  id: ForkTestCartridgeA
  components:
  - type: Cartridge
    programName: fork-test-a

- type: entity
  id: ForkTestCartridgeB
  components:
  - type: Cartridge
    programName: fork-test-b

- type: entity
  id: ForkTestCartridgeC
  components:
  - type: Cartridge
    programName: fork-test-c

- type: entity
  id: ForkTestPdaBasic
  components:
  - type: Pda
  - type: ContainerContainer
    containers:
      program-container: !type:Container
      PDA-id: !type:ContainerSlot {}
      PDA-pen: !type:ContainerSlot {}
      PDA-pai: !type:ContainerSlot {}
      Cartridge-Slot: !type:ContainerSlot {}
  - type: CartridgeLoader
    uiKey: enum.PdaUiKey.Key
    diskSpace: 10
  - type: ItemSlots

- type: entity
  id: ForkTestPdaWithPhysics
  components:
  - type: Pda
  - type: ContainerContainer
    containers:
      program-container: !type:Container
      PDA-id: !type:ContainerSlot {}
      PDA-pen: !type:ContainerSlot {}
      PDA-pai: !type:ContainerSlot {}
      Cartridge-Slot: !type:ContainerSlot {}
  - type: CartridgeLoader
    uiKey: enum.PdaUiKey.Key
    diskSpace: 10
  - type: ItemSlots
  - type: Physics

- type: forkCartridgeSet
  id: ForkTestGlobal
  order: 0
  cartridges:
    - ForkTestCartridgeA

- type: forkCartridgeSet
  id: ForkTestFiltered
  order: 1
  cartridges:
    - ForkTestCartridgeB
  requireComponents:
    - Physics

- type: forkCartridgeSet
  id: ForkTestExcluded
  order: 2
  cartridges:
    - ForkTestCartridgeC
  excludeComponents:
    - Physics
";

    [Test]
    public async Task GlobalCartridgeInstalledTest()
    {
        await SpawnTarget("ForkTestPdaBasic");
        var targetEnt = SEntMan.GetEntity(Target!.Value);
        await RunTicks(5);

        await Server.WaitAssertion(() =>
        {
            var loader = SEntMan.System<CartridgeLoaderSystem>();
            var installed = loader.GetInstalled(targetEnt);
            var protoIds = installed
                .Select(e => SEntMan.GetComponent<MetaDataComponent>(e).EntityPrototype?.ID)
                .ToList();

            Assert.That(protoIds, Does.Contain(TestCartridgeA), "Global cartridge should be installed.");
        });
    }

    [Test]
    public async Task RequireComponentsFilterTest()
    {
        await SpawnTarget("ForkTestPdaBasic");
        var basicEnt = SEntMan.GetEntity(Target!.Value);
        await RunTicks(5);

        await Server.WaitAssertion(() =>
        {
            var loader = SEntMan.System<CartridgeLoaderSystem>();
            var installed = loader.GetInstalled(basicEnt);
            var protoIds = installed
                .Select(e => SEntMan.GetComponent<MetaDataComponent>(e).EntityPrototype?.ID)
                .ToList();

            Assert.That(protoIds, Does.Not.Contain(TestCartridgeB),
                "Filtered cartridge should not be on entity without required component.");
        });

        await SpawnTarget("ForkTestPdaWithPhysics");
        var physicsEnt = SEntMan.GetEntity(Target!.Value);
        await RunTicks(5);

        await Server.WaitAssertion(() =>
        {
            var loader = SEntMan.System<CartridgeLoaderSystem>();
            var installed = loader.GetInstalled(physicsEnt);
            var protoIds = installed
                .Select(e => SEntMan.GetComponent<MetaDataComponent>(e).EntityPrototype?.ID)
                .ToList();

            Assert.That(protoIds, Does.Contain(TestCartridgeB),
                "Filtered cartridge should be on entity with required component.");
        });
    }

    [Test]
    public async Task ExcludeComponentsFilterTest()
    {
        await SpawnTarget("ForkTestPdaBasic");
        var basicEnt = SEntMan.GetEntity(Target!.Value);
        await RunTicks(5);

        await Server.WaitAssertion(() =>
        {
            var loader = SEntMan.System<CartridgeLoaderSystem>();
            var installed = loader.GetInstalled(basicEnt);
            var protoIds = installed
                .Select(e => SEntMan.GetComponent<MetaDataComponent>(e).EntityPrototype?.ID)
                .ToList();

            Assert.That(protoIds, Does.Contain(TestCartridgeC),
                "Excluded cartridge should be on entity without excluded component.");
        });

        await SpawnTarget("ForkTestPdaWithPhysics");
        var physicsEnt = SEntMan.GetEntity(Target!.Value);
        await RunTicks(5);

        await Server.WaitAssertion(() =>
        {
            var loader = SEntMan.System<CartridgeLoaderSystem>();
            var installed = loader.GetInstalled(physicsEnt);
            var protoIds = installed
                .Select(e => SEntMan.GetComponent<MetaDataComponent>(e).EntityPrototype?.ID)
                .ToList();

            Assert.That(protoIds, Does.Not.Contain(TestCartridgeC),
                "Excluded cartridge should not be on entity with excluded component.");
        });
    }

    [Test]
    public async Task DuplicateCartridgeSkippedTest()
    {
        // ForkTestPdaBasic gets ForkTestCartridgeA from the global set.
        // Manually pre-installing it should not result in two copies.
        await SpawnTarget("ForkTestPdaBasic");
        var targetEnt = SEntMan.GetEntity(Target!.Value);
        await RunTicks(5);

        await Server.WaitAssertion(() =>
        {
            var loader = SEntMan.System<CartridgeLoaderSystem>();
            var installed = loader.GetInstalled(targetEnt);
            var count = installed
                .Count(e => SEntMan.GetComponent<MetaDataComponent>(e).EntityPrototype?.ID == TestCartridgeA);

            Assert.That(count, Is.EqualTo(1), "Duplicate cartridge should not be installed twice.");
        });
    }
}
