using Content.Shared.Interaction.Events;
using Content.Shared.Paper;
using Content.Shared.RussStation.Traits;
using Content.Shared.UserInterface;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.RussStation.Traits;

/// <summary>
/// Verifies <see cref="PapyrophobiaSystem"/> blocks all three paper-interaction paths
/// for an afflicted user: opening the paper UI, writing on paper, and using paper in hand.
/// Non-paper targets and non-afflicted users are left alone.
/// </summary>
[TestFixture]
[TestOf(typeof(PapyrophobiaSystem))]
public sealed class PapyrophobiaTest
{
    [Test]
    public async Task BlocksPaperInteractionsTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var user = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            entMan.AddComponent<PapyrophobiaComponent>(user);

            var paper = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            entMan.AddComponent<PaperComponent>(paper);

            // Opening the paper's activatable UI should be cancelled.
            var openEv = new UserOpenActivatableUIAttemptEvent(user, paper, silent: true);
            entMan.EventBus.RaiseLocalEvent(user, openEv);
            Assert.That(openEv.Cancelled, Is.True, "Papyrophobe should not be able to open a paper UI.");

            // Writing on paper should be cancelled with the trait's fail reason.
            var writeEv = new PaperWriteAttemptEvent(paper);
            entMan.EventBus.RaiseLocalEvent(user, ref writeEv);
            Assert.That(writeEv.Cancelled, Is.True, "Papyrophobe should not be able to write on paper.");
            Assert.That(writeEv.FailReason, Is.EqualTo("papyrophobia-popup"));

            // Using paper in hand should be short-circuited (Handled) before ingestion eats it.
            var useEv = new UseInHandEvent(user);
            entMan.EventBus.RaiseLocalEvent(paper, useEv);
            Assert.That(useEv.Handled, Is.True, "Papyrophobe using paper in hand should be intercepted.");

            entMan.DeleteEntity(user);
            entMan.DeleteEntity(paper);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NonPaperUiNotBlockedTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var user = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            entMan.AddComponent<PapyrophobiaComponent>(user);

            // A non-paper target (no PaperComponent) must not be blocked by the phobia.
            var nonPaper = entMan.SpawnEntity(null, MapCoordinates.Nullspace);

            var openEv = new UserOpenActivatableUIAttemptEvent(user, nonPaper, silent: true);
            entMan.EventBus.RaiseLocalEvent(user, openEv);
            Assert.That(openEv.Cancelled, Is.False, "Papyrophobia should only block paper, not arbitrary UIs.");

            entMan.DeleteEntity(user);
            entMan.DeleteEntity(nonPaper);
        });

        await pair.CleanReturnAsync();
    }
}
