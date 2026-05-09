using Content.IntegrationTests.Fixtures;
using Content.Shared.ActionBlocker;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.RussStation.EscalatedGrab;
using Content.Shared.RussStation.EscalatedGrab.Components;
using Content.Shared.RussStation.EscalatedGrab.Events;
using Content.Shared.RussStation.EscalatedGrab.Systems;
using Content.Shared.Strip.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.RussStation.EscalatedGrab;

[TestOf(typeof(SharedEscalatedGrabSystem))]
public sealed class EscalatedGrabTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: GrabTestMob
  components:
  - type: Hands
  - type: ComplexInteraction
  - type: InputMover
  - type: Physics
    bodyType: KinematicController
  - type: Puller
  - type: DoAfter
  - type: Damageable
  - type: Fixtures
    fixtures:
      fix1:
        shape:
          !type:PhysShapeCircle
          radius: 0.35

- type: entity
  id: GrabTestTarget
  components:
  - type: Physics
    bodyType: KinematicController
  - type: Pullable
  - type: DoAfter
  - type: InputMover
  - type: Stamina
  - type: Fixtures
    fixtures:
      fix1:
        shape:
          !type:PhysShapeCircle
          radius: 0.35
";

    [Test]
    public async Task DefaultStageIsPull()
    {
        var server = Server;
        var entMan = server.EntMan;

        await server.WaitPost(() =>
        {
            var puller = entMan.SpawnEntity("GrabTestMob", MapCoordinates.Nullspace);
            var state = entMan.AddComponent<GrabStateComponent>(puller);

            Assert.That(state.Stage, Is.EqualTo(GrabStage.Pull));

            entMan.DeleteEntity(puller);
        });
    }

    [Test]
    public async Task GetStageReturnsCorrectStage()
    {
        var server = Server;
        var entMan = server.EntMan;
        var grabSystem = entMan.System<SharedEscalatedGrabSystem>();

        await server.WaitPost(() =>
        {
            var puller = entMan.SpawnEntity("GrabTestMob", MapCoordinates.Nullspace);
            var target = entMan.SpawnEntity("GrabTestTarget", MapCoordinates.Nullspace);

            var state = entMan.EnsureComponent<GrabStateComponent>(puller);
            state.Target = target;
            state.Stage = GrabStage.Aggressive;

            Assert.That(grabSystem.GetStage(puller, target), Is.EqualTo(GrabStage.Aggressive));

            entMan.DeleteEntity(puller);
            entMan.DeleteEntity(target);
        });
    }

    [Test]
    public async Task GetStageReturnsPullForDifferentTarget()
    {
        var server = Server;
        var entMan = server.EntMan;
        var grabSystem = entMan.System<SharedEscalatedGrabSystem>();

        await server.WaitPost(() =>
        {
            var puller = entMan.SpawnEntity("GrabTestMob", MapCoordinates.Nullspace);
            var target1 = entMan.SpawnEntity("GrabTestTarget", MapCoordinates.Nullspace);
            var target2 = entMan.SpawnEntity("GrabTestTarget", MapCoordinates.Nullspace);

            var state = entMan.EnsureComponent<GrabStateComponent>(puller);
            state.Target = target1;
            state.Stage = GrabStage.Choke;

            // Querying a different target returns Pull (no escalation on that target).
            Assert.That(grabSystem.GetStage(puller, target2), Is.EqualTo(GrabStage.Pull));

            entMan.DeleteEntity(puller);
            entMan.DeleteEntity(target1);
            entMan.DeleteEntity(target2);
        });
    }

    [Test]
    public async Task HasStageChecksMinimum()
    {
        var server = Server;
        var entMan = server.EntMan;
        var grabSystem = entMan.System<SharedEscalatedGrabSystem>();

        await server.WaitPost(() =>
        {
            var puller = entMan.SpawnEntity("GrabTestMob", MapCoordinates.Nullspace);
            var target = entMan.SpawnEntity("GrabTestTarget", MapCoordinates.Nullspace);

            var state = entMan.EnsureComponent<GrabStateComponent>(puller);
            state.Target = target;
            state.Stage = GrabStage.Aggressive;

            Assert.Multiple(() =>
            {
                Assert.That(grabSystem.HasStage(puller, target, GrabStage.Pull), Is.True);
                Assert.That(grabSystem.HasStage(puller, target, GrabStage.Grab), Is.True);
                Assert.That(grabSystem.HasStage(puller, target, GrabStage.Aggressive), Is.True);
                Assert.That(grabSystem.HasStage(puller, target, GrabStage.Choke), Is.False);
            });

            entMan.DeleteEntity(puller);
            entMan.DeleteEntity(target);
        });
    }

    [Test]
    public async Task ClearEscalationRemovesComponent()
    {
        var server = Server;
        var entMan = server.EntMan;
        var grabSystem = entMan.System<SharedEscalatedGrabSystem>();

        await server.WaitPost(() =>
        {
            var puller = entMan.SpawnEntity("GrabTestMob", MapCoordinates.Nullspace);
            var target = entMan.SpawnEntity("GrabTestTarget", MapCoordinates.Nullspace);

            var state = entMan.EnsureComponent<GrabStateComponent>(puller);
            state.Target = target;
            state.Stage = GrabStage.Aggressive;

            grabSystem.ClearEscalation(puller);

            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<GrabStateComponent>(puller), Is.False);
                Assert.That(grabSystem.GetStage(puller, target), Is.EqualTo(GrabStage.Pull));
            });

            entMan.DeleteEntity(puller);
            entMan.DeleteEntity(target);
        });
    }

    [Test]
    public async Task TryEscalateCreatesStateWithTarget()
    {
        var server = Server;
        var entMan = server.EntMan;
        var grabSystem = entMan.System<SharedEscalatedGrabSystem>();

        await server.WaitPost(() =>
        {
            var puller = entMan.SpawnEntity("GrabTestMob", MapCoordinates.Nullspace);
            var target = entMan.SpawnEntity("GrabTestTarget", MapCoordinates.Nullspace);

            // TryEscalate should create the GrabStateComponent and set the target.
            var result = grabSystem.TryEscalate(puller, target);
            Assert.That(result, Is.True);

            var state = entMan.GetComponent<GrabStateComponent>(puller);
            Assert.Multiple(() =>
            {
                Assert.That(state.Target, Is.EqualTo(target));
                // Stage stays at Pull until the do-after completes.
                Assert.That(state.Stage, Is.EqualTo(GrabStage.Pull));
            });

            entMan.DeleteEntity(puller);
            entMan.DeleteEntity(target);
        });
    }

    [Test]
    public async Task GrabStageBlocksTargetMovement()
    {
        var server = Server;
        var entMan = server.EntMan;
        var actionBlocker = entMan.System<ActionBlockerSystem>();
        var pullSystem = entMan.System<PullingSystem>();
        var handSystem = entMan.System<SharedHandsSystem>();

        var map = await Pair.CreateTestMap();
        var coords = map.MapCoords;

        await server.WaitPost(() =>
        {
            var puller = entMan.SpawnEntity("GrabTestMob", coords);
            var target = entMan.SpawnEntity("GrabTestTarget", coords);

            handSystem.AddHand(puller, "hand", HandLocation.Left);

            Assert.That(pullSystem.TryStartPull(puller, target), Is.True);

            // At Pull stage, target can still move.
            actionBlocker.UpdateCanMove(target);
            Assert.That(actionBlocker.CanMove(target), Is.True, "Target should move at Pull stage");

            // Escalate to Grab - target movement should be blocked.
            var state = entMan.EnsureComponent<GrabStateComponent>(puller);
            state.Target = target;
            state.Stage = GrabStage.Grab;
            actionBlocker.UpdateCanMove(target);
            Assert.That(actionBlocker.CanMove(target), Is.False, "Target should not move at Grab stage");

            // Escalate to Aggressive - still blocked.
            state.Stage = GrabStage.Aggressive;
            actionBlocker.UpdateCanMove(target);
            Assert.That(actionBlocker.CanMove(target), Is.False, "Target should not move at Aggressive stage");

            entMan.DeleteEntity(puller);
            entMan.DeleteEntity(target);
        });
    }

    [Test]
    public async Task ClearEscalationRestoresTargetMovement()
    {
        var server = Server;
        var entMan = server.EntMan;
        var actionBlocker = entMan.System<ActionBlockerSystem>();
        var pullSystem = entMan.System<PullingSystem>();
        var grabSystem = entMan.System<SharedEscalatedGrabSystem>();
        var handSystem = entMan.System<SharedHandsSystem>();

        var map = await Pair.CreateTestMap();
        var coords = map.MapCoords;

        await server.WaitPost(() =>
        {
            var puller = entMan.SpawnEntity("GrabTestMob", coords);
            var target = entMan.SpawnEntity("GrabTestTarget", coords);

            handSystem.AddHand(puller, "hand", HandLocation.Left);
            pullSystem.TryStartPull(puller, target);

            var state = entMan.EnsureComponent<GrabStateComponent>(puller);
            state.Target = target;
            state.Stage = GrabStage.Grab;
            actionBlocker.UpdateCanMove(target);
            Assert.That(actionBlocker.CanMove(target), Is.False, "Movement should be blocked at Grab");

            // Clear escalation - target should be able to move again.
            grabSystem.ClearEscalation(puller);
            Assert.That(actionBlocker.CanMove(target), Is.True, "Movement should be restored after clearing escalation");

            entMan.DeleteEntity(puller);
            entMan.DeleteEntity(target);
        });
    }

    [Test]
    public async Task PullerSpeedModifiedByGrabStage()
    {
        var server = Server;
        var entMan = server.EntMan;

        await server.WaitPost(() =>
        {
            var puller = entMan.SpawnEntity("GrabTestMob", MapCoordinates.Nullspace);

            var state = entMan.EnsureComponent<GrabStateComponent>(puller);
            state.Stage = GrabStage.Grab;

            // Raise the event directly to check the modifier is applied.
            var ev = new RefreshMovementSpeedModifiersEvent();
            entMan.EventBus.RaiseLocalEvent(puller, ev);

            var expected = GrabStateComponent.PullerSpeedModifiers[(int) GrabStage.Grab];
            Assert.Multiple(() =>
            {
                Assert.That(ev.WalkSpeedModifier, Is.EqualTo(expected).Within(0.001f), "Walk speed modifier should match Grab stage");
                Assert.That(ev.SprintSpeedModifier, Is.EqualTo(expected).Within(0.001f), "Sprint speed modifier should match Grab stage");
            });

            entMan.DeleteEntity(puller);
        });
    }

    [Test]
    public async Task DropStageReducesFromAggressiveToGrab()
    {
        var server = Server;
        var entMan = server.EntMan;
        var grabSystem = entMan.System<SharedEscalatedGrabSystem>();

        await server.WaitPost(() =>
        {
            var puller = entMan.SpawnEntity("GrabTestMob", MapCoordinates.Nullspace);
            var target = entMan.SpawnEntity("GrabTestTarget", MapCoordinates.Nullspace);

            var state = entMan.EnsureComponent<GrabStateComponent>(puller);
            state.Target = target;
            state.Stage = GrabStage.Aggressive;

            grabSystem.DropStage(puller, state);

            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<GrabStateComponent>(puller), Is.True);
                Assert.That(state.Stage, Is.EqualTo(GrabStage.Grab));
            });

            entMan.DeleteEntity(puller);
            entMan.DeleteEntity(target);
        });
    }

    [Test]
    public async Task DropStageAtGrabClearsEscalation()
    {
        var server = Server;
        var entMan = server.EntMan;
        var grabSystem = entMan.System<SharedEscalatedGrabSystem>();

        await server.WaitPost(() =>
        {
            var puller = entMan.SpawnEntity("GrabTestMob", MapCoordinates.Nullspace);
            var target = entMan.SpawnEntity("GrabTestTarget", MapCoordinates.Nullspace);

            var state = entMan.EnsureComponent<GrabStateComponent>(puller);
            state.Target = target;
            state.Stage = GrabStage.Grab;

            grabSystem.DropStage(puller, state);

            Assert.That(entMan.HasComponent<GrabStateComponent>(puller), Is.False,
                "GrabStateComponent should be removed when dropping from Grab stage");

            entMan.DeleteEntity(puller);
            entMan.DeleteEntity(target);
        });
    }

    [Test]
    public async Task TryResistStartsDoAfter()
    {
        var server = Server;
        var entMan = server.EntMan;
        var grabSystem = entMan.System<SharedEscalatedGrabSystem>();

        var map = await Pair.CreateTestMap();
        var coords = map.MapCoords;

        await server.WaitPost(() =>
        {
            var puller = entMan.SpawnEntity("GrabTestMob", coords);
            var target = entMan.SpawnEntity("GrabTestTarget", coords);

            var state = entMan.EnsureComponent<GrabStateComponent>(puller);
            state.Target = target;
            state.Stage = GrabStage.Aggressive;

            grabSystem.TryResist(target, puller, state);

            Assert.Multiple(() =>
            {
                Assert.That(state.ResistDoAfter, Is.Not.Null, "Resist do-after should be started");
                // Stage should not change yet (do-after still in progress).
                Assert.That(state.Stage, Is.EqualTo(GrabStage.Aggressive));
            });

            // Clean up do-afters before deleting entities.
            grabSystem.ClearEscalation(puller);
            entMan.DeleteEntity(puller);
            entMan.DeleteEntity(target);
        });
    }

    [Test]
    public async Task ChokeStageAppliesStaminaDamage()
    {
        var server = Server;
        var entMan = server.EntMan;
        var grabSystem = entMan.System<SharedEscalatedGrabSystem>();

        var map = await Pair.CreateTestMap();
        var coords = map.MapCoords;

        await server.WaitPost(() =>
        {
            var puller = entMan.SpawnEntity("GrabTestMob", coords);
            var target = entMan.SpawnEntity("GrabTestTarget", coords);

            var state = entMan.EnsureComponent<GrabStateComponent>(puller);
            state.Target = target;
            state.Stage = GrabStage.Choke;

            var stamina = entMan.GetComponent<StaminaComponent>(target);
            var initialStamina = stamina.StaminaDamage;

            // Simulate enough time for at least one tick (0.5s interval).
            grabSystem.Update(1.0f);

            Assert.That(stamina.StaminaDamage, Is.GreaterThan(initialStamina),
                "Target should take stamina damage during choke");

            grabSystem.ClearEscalation(puller);
            entMan.DeleteEntity(puller);
            entMan.DeleteEntity(target);
        });
    }

    [Test]
    public async Task DamageAboveThresholdDropsGrabStage()
    {
        var server = Server;
        var entMan = server.EntMan;

        await server.WaitPost(() =>
        {
            var puller = entMan.SpawnEntity("GrabTestMob", MapCoordinates.Nullspace);
            var target = entMan.SpawnEntity("GrabTestTarget", MapCoordinates.Nullspace);

            var state = entMan.EnsureComponent<GrabStateComponent>(puller);
            state.Target = target;
            state.Stage = GrabStage.Aggressive;

            // Raise DamageChangedEvent with damage above threshold (15).
            var damageable = entMan.GetComponent<DamageableComponent>(puller);
            var damageSpec = new DamageSpecifier();
            damageSpec.DamageDict["Blunt"] = FixedPoint2.New(20);
            var ev = new DamageChangedEvent(damageable, damageSpec, true, null);
            entMan.EventBus.RaiseLocalEvent(puller, ev);

            Assert.That(state.Stage, Is.EqualTo(GrabStage.Grab),
                "Stage should drop from Aggressive to Grab after taking heavy damage");

            entMan.DeleteEntity(puller);
            entMan.DeleteEntity(target);
        });
    }

    [Test]
    public async Task DamageBelowThresholdKeepsGrabStage()
    {
        var server = Server;
        var entMan = server.EntMan;

        await server.WaitPost(() =>
        {
            var puller = entMan.SpawnEntity("GrabTestMob", MapCoordinates.Nullspace);
            var target = entMan.SpawnEntity("GrabTestTarget", MapCoordinates.Nullspace);

            var state = entMan.EnsureComponent<GrabStateComponent>(puller);
            state.Target = target;
            state.Stage = GrabStage.Aggressive;

            // Raise DamageChangedEvent with damage below threshold (15).
            var damageable = entMan.GetComponent<DamageableComponent>(puller);
            var damageSpec = new DamageSpecifier();
            damageSpec.DamageDict["Blunt"] = FixedPoint2.New(10);
            var ev = new DamageChangedEvent(damageable, damageSpec, true, null);
            entMan.EventBus.RaiseLocalEvent(puller, ev);

            Assert.That(state.Stage, Is.EqualTo(GrabStage.Aggressive),
                "Stage should remain Aggressive when damage is below threshold");

            entMan.DeleteEntity(puller);
            entMan.DeleteEntity(target);
        });
    }

    [Test]
    public async Task StripTimeReducedAtHigherGrabStages()
    {
        var server = Server;
        var entMan = server.EntMan;
        var pullSystem = entMan.System<PullingSystem>();
        var handSystem = entMan.System<SharedHandsSystem>();

        var map = await Pair.CreateTestMap();
        var coords = map.MapCoords;

        await server.WaitPost(() =>
        {
            var puller = entMan.SpawnEntity("GrabTestMob", coords);
            var target = entMan.SpawnEntity("GrabTestTarget", coords);

            handSystem.AddHand(puller, "hand", HandLocation.Left);
            pullSystem.TryStartPull(puller, target);

            var state = entMan.EnsureComponent<GrabStateComponent>(puller);
            state.Target = target;
            state.Stage = GrabStage.Aggressive;

            // Raise BeforeGettingStrippedEvent on target and check multiplier.
            var baseTime = TimeSpan.FromSeconds(5);
            var stripEv = new BeforeGettingStrippedEvent(baseTime);
            entMan.EventBus.RaiseLocalEvent(target, ref stripEv);

            var expected = GrabStateComponent.StripTimeModifiers[(int) GrabStage.Aggressive];
            Assert.That(stripEv.Multiplier, Is.EqualTo(expected).Within(0.001f),
                "Strip time multiplier should match Aggressive stage modifier");

            entMan.DeleteEntity(puller);
            entMan.DeleteEntity(target);
        });
    }

    [Test]
    public async Task PushoverQuirkIncreasesResistTime()
    {
        var server = Server;
        var entMan = server.EntMan;

        await server.WaitPost(() =>
        {
            var puller = entMan.SpawnEntity("GrabTestMob", MapCoordinates.Nullspace);
            var target = entMan.SpawnEntity("GrabTestTarget", MapCoordinates.Nullspace);

            var pushover = entMan.AddComponent<PushoverComponent>(target);

            var baseResistTime = TimeSpan.FromSeconds(5);
            var ev = new GrabResistAttemptEvent(puller, target, GrabStage.Aggressive, baseResistTime);
            entMan.EventBus.RaiseLocalEvent(target, ref ev);

            var expected = baseResistTime * pushover.ResistTimeMultiplier;
            Assert.That(ev.ResistTime, Is.EqualTo(expected),
                "Pushover quirk should multiply resist time by ResistTimeMultiplier");

            entMan.DeleteEntity(puller);
            entMan.DeleteEntity(target);
        });
    }

    [Test]
    public async Task ResistTimeUnchangedWithoutPushover()
    {
        var server = Server;
        var entMan = server.EntMan;

        await server.WaitPost(() =>
        {
            var puller = entMan.SpawnEntity("GrabTestMob", MapCoordinates.Nullspace);
            var target = entMan.SpawnEntity("GrabTestTarget", MapCoordinates.Nullspace);

            var baseResistTime = TimeSpan.FromSeconds(5);
            var ev = new GrabResistAttemptEvent(puller, target, GrabStage.Aggressive, baseResistTime);
            entMan.EventBus.RaiseLocalEvent(target, ref ev);

            Assert.That(ev.ResistTime, Is.EqualTo(baseResistTime),
                "Resist time should be unchanged without Pushover component");

            entMan.DeleteEntity(puller);
            entMan.DeleteEntity(target);
        });
    }
}
