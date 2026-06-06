using Content.Shared.ActionBlocker;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.RussStation.EscalatedGrab;
using Content.Shared.RussStation.EscalatedGrab.Components;
using Content.Shared.RussStation.EscalatedGrab.Systems;
using Content.Shared.Strip.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.RussStation.EscalatedGrab;

/// <summary>
/// Movement, speed, and strip-time effects driven by the current grab stage.
/// Shares the <c>Prototypes</c> fixture declared in <c>EscalatedGrabTest.cs</c>.
/// </summary>
public sealed partial class EscalatedGrabTest
{
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
}
