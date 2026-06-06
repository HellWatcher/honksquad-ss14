using Content.IntegrationTests.Fixtures;
using Content.Shared.RussStation.EscalatedGrab;
using Content.Shared.RussStation.EscalatedGrab.Components;
using Content.Shared.RussStation.EscalatedGrab.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.RussStation.EscalatedGrab;

/// <summary>
/// Tests for the escalated grab system. The stage state/query scenarios live here;
/// movement and speed effects live in <c>EscalatedGrabTest.Movement.cs</c>, and the
/// combat/resist scenarios live in <c>EscalatedGrabTest.Combat.cs</c>. All partials
/// share the <see cref="Prototypes"/> fixture below.
/// </summary>
[TestOf(typeof(SharedEscalatedGrabSystem))]
public sealed partial class EscalatedGrabTest : GameTest
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
}
