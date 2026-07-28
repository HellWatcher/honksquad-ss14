using Content.IntegrationTests.Fixtures;
using Content.Server.RussStation.DoAfterCancel;
using Content.Shared.DoAfter;
using Content.Shared.RussStation.DoAfterCancel;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.RussStation.DoAfterCancel;

/// <summary>
/// Verifies <see cref="DoAfterCancelSystem"/>: when the player sends
/// <see cref="CancelAllDoAftersEvent"/> (their Escape press), only DoAfters the player
/// started themselves are cancelled. Hostile DoAfters where the player is merely the
/// target live on the attacker's component and keep running.
/// </summary>
[TestFixture]
[TestOf(typeof(DoAfterCancelSystem))]
public sealed partial class DoAfterCancelTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: DoAfterCancelDummy
  components:
  - type: DoAfter
";

    [Serializable, NetSerializable]
    private sealed partial class TestDoAfterEvent : DoAfterEvent
    {
        public override DoAfterEvent Clone() => this;
    }

    [Test]
    public async Task CancelsOwnDoAfterButNotHostileTest()
    {
        var server = Server;
        var entMan = server.EntMan;
        var timing = server.ResolveDependency<IGameTiming>();
        var doAfterSys = entMan.System<SharedDoAfterSystem>();

        var ownEvent = new TestDoAfterEvent();
        var hostileEvent = new TestDoAfterEvent();
        DoAfterId? hostileId = null;

        // Long enough that neither DoAfter completes on its own during the test.
        var longDelay = timing.TickPeriod * 1000;

        await server.WaitPost(() =>
        {
            var player = entMan.SpawnEntity("DoAfterCancelDummy", MapCoordinates.Nullspace);
            var attacker = entMan.SpawnEntity("DoAfterCancelDummy", MapCoordinates.Nullspace);

            // Make the player session control the player entity, so the server sees it as
            // the sender of the cancel event.
            server.PlayerMan.SetAttachedEntity(ServerSession, player, force: true);

            // A DoAfter the player started themselves.
            var ownArgs = new DoAfterArgs(entMan, player, longDelay, ownEvent, null) { Broadcast = true };
            Assert.That(doAfterSys.TryStartDoAfter(ownArgs), Is.True);

            // A hostile DoAfter run by the attacker, targeting the player. It is stored on the
            // attacker's DoAfterComponent, not the player's.
            var hostileArgs = new DoAfterArgs(entMan, attacker, longDelay, hostileEvent, null, player) { Broadcast = true };
            Assert.That(doAfterSys.TryStartDoAfter(hostileArgs, out hostileId), Is.True);

            Assert.That(ownEvent.Cancelled, Is.False);
            Assert.That(hostileEvent.Cancelled, Is.False);
        });

        // The client presses Escape: send the cancel event from the client to the server.
        await Client.WaitPost(() =>
            Client.EntMan.EntityNetManager.SendSystemNetworkMessage(new CancelAllDoAftersEvent()));

        await Pair.RunTicksSync(15);

        Assert.Multiple(() =>
        {
            Assert.That(ownEvent.Cancelled, Is.True,
                "The player's own DoAfter should be cancelled by the Escape key.");
            Assert.That(hostileEvent.Cancelled, Is.False,
                "A hostile DoAfter targeting the player should keep running.");
        });

        // The hostile DoAfter is still running by design, which is the whole point of the
        // assertion above. Leaving it running past the end of the test leaks it into the
        // pooled server: recycling deletes these dummies, and ShouldCancel then calls
        // Transform() on the deleted movement entity and logs a resolve error, which the
        // pool blames on whichever test borrows the pair next. Cancel it so the leak stops
        // here. See the note on SharedDoAfterSystem.ShouldCancel in the PR description --
        // upstream dropped the TryComp guards this window, so the unguarded Transform is
        // an upstream regression, not something this test can fix.
        await server.WaitPost(() => doAfterSys.Cancel(hostileId));
    }
}
