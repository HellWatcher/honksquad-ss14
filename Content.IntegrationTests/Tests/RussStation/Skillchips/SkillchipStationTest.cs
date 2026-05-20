using System;
using Content.Server.Power.Components;
using Content.Server.RussStation.Skillchips;
using Content.Shared.Body;
using Content.Shared.Movement.Events;
using Content.Shared.RussStation.Skillchips;
using Content.Shared.RussStation.Skillchips.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.RussStation.Skillchips;

/// <summary>
/// Behavioural tests for the SkillchipStation machine: implant fast-fails on
/// duplicates and unpowered state, the implant and removal DoAfters complete
/// end-to-end, and the occupant is ejected when they relay-move out of the
/// body container.
/// </summary>
[TestFixture]
[TestOf(typeof(SkillchipStationSystem))]
public sealed class SkillchipStationTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: skillchip
  id: StationTestChip
  name: Station Test Chip
  capacityCost: 1

- type: entity
  id: StationTestBrain
  components:
  - type: Brain
  - type: Organ
    category: Brain
  - type: SkillchipHolder

- type: entity
  id: StationTestBody
  components:
  - type: Body

- type: entity
  id: StationTestOperator
  components:
  - type: Transform

- type: entity
  id: StationTestChipItem
  components:
  - type: Skillchip
    chipProto: StationTestChip

- type: entity
  id: TestSkillchipStation
  components:
  - type: SkillchipStation
  - type: Appearance
  - type: ContainerContainer
    containers:
      skillchip_slot: !type:ContainerSlot {}
      scanner-body: !type:ContainerSlot {}
";

    /// <summary>
    /// Drops a chip into a brain that lives inside a body, with the body
    /// inside the station's occupant slot, ready for implant/remove flows.
    /// </summary>
    private static (EntityUid station, EntityUid operatorEnt, EntityUid body, EntityUid brain, EntityUid chip)
        SetupSeated(IEntityManager em, SharedContainerSystem containers, MapCoordinates coords, bool seatBody = true)
    {
        var station = em.SpawnEntity("TestSkillchipStation", coords);
        var operatorEnt = em.SpawnEntity("StationTestOperator", coords);
        var body = em.SpawnEntity("StationTestBody", coords);
        var brain = em.SpawnEntity("StationTestBrain", coords);
        var chip = em.SpawnEntity("StationTestChipItem", coords);

        // Brain into body's organ container.
        var organContainer = containers.EnsureContainer<Container>(body, BodyComponent.ContainerID);
        containers.Insert(brain, organContainer);

        // Chip into the station tray.
        var tray = containers.EnsureContainer<ContainerSlot>(station, SkillchipStationComponent.ChipSlotId);
        containers.Insert(chip, tray);

        if (seatBody)
        {
            var seat = containers.EnsureContainer<ContainerSlot>(station, SkillchipStationComponent.BodyContainerId);
            containers.Insert(body, seat);
        }

        return (station, operatorEnt, body, brain, chip);
    }

    /// <summary>
    /// Patient already has the chip installed: the implant message must not
    /// start a procedure and must leave the tray chip untouched.
    /// </summary>
    [Test]
    public async Task ImplantMessage_DuplicateChip_DoesNothing()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var em = server.ResolveDependency<IEntityManager>();
        var skillchips = server.System<SharedSkillchipSystem>();
        var containers = server.System<SharedContainerSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var (station, op, _, brain, chip) = SetupSeated(em, containers, mapData.MapCoords);

            // Pre-install the chip so the next attempt is a duplicate.
            var holder = em.GetComponent<SkillchipHolderComponent>(brain);
            Assert.That(skillchips.TryInstall((brain, holder), "StationTestChip"), Is.True);

            var msg = new SkillchipStationImplantMessage { Actor = op };
            em.EventBus.RaiseLocalEvent(station, msg);

            var tray = containers.EnsureContainer<ContainerSlot>(station, SkillchipStationComponent.ChipSlotId);
            Assert.That(tray.ContainedEntity, Is.EqualTo(chip),
                "Tray chip should not be consumed when the patient already has the chip");
            Assert.That(holder.ImplantedChips.Count, Is.EqualTo(1),
                "Duplicate fast-fail should leave the existing chip alone, not double-install");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// With ApcPowerReceiver present but no power source connected the
    /// station counts as unpowered and the implant flow must refuse.
    /// </summary>
    [Test]
    public async Task ImplantMessage_Unpowered_DoesNothing()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var em = server.ResolveDependency<IEntityManager>();
        var skillchips = server.System<SharedSkillchipSystem>();
        var containers = server.System<SharedContainerSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var (station, op, _, brain, chip) = SetupSeated(em, containers, mapData.MapCoords);

            // No provider on the test map, so the receiver reports unpowered.
            em.AddComponent<ApcPowerReceiverComponent>(station);

            var msg = new SkillchipStationImplantMessage { Actor = op };
            em.EventBus.RaiseLocalEvent(station, msg);

            var tray = containers.EnsureContainer<ContainerSlot>(station, SkillchipStationComponent.ChipSlotId);
            Assert.That(tray.ContainedEntity, Is.EqualTo(chip),
                "Unpowered station should refuse implant and keep the tray chip");
            Assert.That(skillchips.HasChipInstalled(brain, "StationTestChip"), Is.False,
                "Unpowered station should not have implanted the chip into the patient");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Happy path: implant DoAfter runs to completion, the tray chip is
    /// consumed, and the chip is recorded on the seated patient's brain.
    /// </summary>
    [Test]
    public async Task ImplantDoAfter_CompletesAndConsumesChip()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var em = server.ResolveDependency<IEntityManager>();
        var skillchips = server.System<SharedSkillchipSystem>();
        var containers = server.System<SharedContainerSystem>();
        var mapData = await pair.CreateTestMap();

        EntityUid station = default, brain = default, chip = default;

        await server.WaitAssertion(() =>
        {
            var setup = SetupSeated(em, containers, mapData.MapCoords);
            station = setup.station;
            brain = setup.brain;
            chip = setup.chip;

            // Shorten the operation so the test does not have to advance
            // hundreds of ticks; OperationDuration is a plain DataField.
            em.GetComponent<SkillchipStationComponent>(station).OperationDuration = TimeSpan.FromSeconds(0.1);

            var msg = new SkillchipStationImplantMessage { Actor = setup.operatorEnt };
            em.EventBus.RaiseLocalEvent(station, msg);
        });

        // 0.1s operation, ~6 ticks at 60Hz; 30 is plenty of headroom.
        await server.WaitRunTicks(30);

        await server.WaitAssertion(() =>
        {
            var tray = containers.EnsureContainer<ContainerSlot>(station, SkillchipStationComponent.ChipSlotId);
            Assert.That(tray.ContainedEntity, Is.Null,
                "Tray chip should be consumed once the implant DoAfter completes");
            Assert.That(em.EntityExists(chip), Is.False,
                "Consumed chip item should be queued for deletion");
            Assert.That(skillchips.HasChipInstalled(brain, "StationTestChip"), Is.True,
                "Brain should record the implanted chip after the DoAfter completes");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Removal flow: with the patient seated and the chip already installed,
    /// the remove DoAfter strips it from the brain.
    /// </summary>
    [Test]
    public async Task RemoveDoAfter_CompletesAndRemovesChip()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var em = server.ResolveDependency<IEntityManager>();
        var skillchips = server.System<SharedSkillchipSystem>();
        var containers = server.System<SharedContainerSystem>();
        var mapData = await pair.CreateTestMap();

        EntityUid station = default, brain = default;

        await server.WaitAssertion(() =>
        {
            var setup = SetupSeated(em, containers, mapData.MapCoords);
            station = setup.station;
            brain = setup.brain;

            em.GetComponent<SkillchipStationComponent>(station).OperationDuration = TimeSpan.FromSeconds(0.1);

            // Pre-install so the remove path has something to act on.
            var holder = em.GetComponent<SkillchipHolderComponent>(brain);
            Assert.That(skillchips.TryInstall((brain, holder), "StationTestChip"), Is.True);

            var msg = new SkillchipStationRemoveMessage("StationTestChip") { Actor = setup.operatorEnt };
            em.EventBus.RaiseLocalEvent(station, msg);
        });

        await server.WaitRunTicks(30);

        await server.WaitAssertion(() =>
        {
            Assert.That(skillchips.HasChipInstalled(brain, "StationTestChip"), Is.False,
                "Chip should be gone from the brain after the remove DoAfter completes");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// When the occupant tries to move out of the body container, the relay
    /// event must trigger an eject and clear the seat.
    /// </summary>
    [Test]
    public async Task RelayMovement_EjectsOccupant()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var em = server.ResolveDependency<IEntityManager>();
        var containers = server.System<SharedContainerSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var (station, _, body, _, _) = SetupSeated(em, containers, mapData.MapCoords);

            var seat = containers.EnsureContainer<ContainerSlot>(station, SkillchipStationComponent.BodyContainerId);
            Assert.That(seat.ContainedEntity, Is.EqualTo(body), "Setup precondition: body seated");

            var relay = new ContainerRelayMovementEntityEvent(body);
            em.EventBus.RaiseLocalEvent(station, ref relay);

            Assert.That(seat.ContainedEntity, Is.Null,
                "Relay movement should eject the occupant from the body container");
        });

        await pair.CleanReturnAsync();
    }
}
