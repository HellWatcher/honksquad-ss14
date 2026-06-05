using Content.IntegrationTests.Fixtures;
using Content.Server.RussStation.UI;
using Robust.Shared.Player;

namespace Content.IntegrationTests.Tests.RussStation.UI;

/// <summary>
/// Verifies <see cref="TrackedDialogSystem"/>'s per-session dedup: a session can only have one
/// tracked dialog open at a time, a repeat open is silently ignored, and cancelling clears the
/// tracking state.
/// </summary>
[TestFixture]
[TestOf(typeof(TrackedDialogSystem))]
public sealed class TrackedDialogTest : GameTest
{
    [Test]
    public async Task DialogIsTrackedAndDedupedPerSessionTest()
    {
        var tracked = Server.EntMan.System<TrackedDialogSystem>();
        var session = ServerSession;
        Assert.That(session, Is.Not.Null, "Test requires a connected player session.");

        await Server.WaitPost(() =>
        {
            Assert.That(tracked.HasPendingDialog(session!), Is.False, "No dialog should be pending initially.");

            tracked.OpenDialog<string>(session!, "Title", "Prompt", _ => { });
            Assert.That(tracked.HasPendingDialog(session!), Is.True, "Opening a dialog should mark it pending.");

            // A repeat open while one is already pending must be ignored, not stacked.
            tracked.OpenDialog<string>(session!, "Title", "Prompt", _ => { });
            Assert.That(tracked.HasPendingDialog(session!), Is.True);

            // A single cancel clears tracking, proving only one dialog was ever tracked.
            tracked.CancelDialog(session!);
            Assert.That(tracked.HasPendingDialog(session!), Is.False, "Cancelling should clear the pending state.");
        });
    }
}
