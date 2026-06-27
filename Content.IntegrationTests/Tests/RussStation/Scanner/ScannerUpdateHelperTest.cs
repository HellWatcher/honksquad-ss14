using System.Numerics;
using Content.Shared.RussStation.Scanner;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.RussStation.Scanner;

[TestFixture]
[TestOf(typeof(ScannerUpdateHelper))]
public sealed class ScannerUpdateHelperTest
{
    /// <summary>
    /// Pins the shared scan-loop decision table: rate-limit, drop a gone target, pause out of range,
    /// otherwise push. Also checks the timer-advance rule, which is the easy thing to get wrong when
    /// copying the loop: the timer only moves once the rate-limit passes and the target is still
    /// valid, so a deleted target leaves it untouched (matching upstream HealthAnalyzerSystem).
    /// </summary>
    [Test]
    public async Task EvaluateDecisionTableTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var transform = entMan.System<SharedTransformSystem>();

            var now = TimeSpan.FromSeconds(10);
            var interval = TimeSpan.FromSeconds(1);
            var due = now - TimeSpan.FromSeconds(1);    // update timer already elapsed
            var notDue = now + TimeSpan.FromSeconds(5); // still rate-limited
            var advanced = now + interval;

            var coords = mapData.GridCoords;
            var farCoords = coords.Offset(new Vector2(1000f, 0f));

            var target = entMan.SpawnEntity(null, coords);

            // No target -> Idle, timer untouched.
            var noTarget = ScannerUpdateHelper.Evaluate(transform, now, null, due, interval,
                null, coords, _ => false, _ => coords);
            Assert.That(noTarget.Action, Is.EqualTo(ScannerUpdateHelper.ScanAction.Idle));
            Assert.That(noTarget.NextUpdate, Is.EqualTo(due));

            // Rate-limited -> Idle, timer untouched.
            var rateLimited = ScannerUpdateHelper.Evaluate(transform, now, target, notDue, interval,
                null, coords, _ => false, _ => coords);
            Assert.That(rateLimited.Action, Is.EqualTo(ScannerUpdateHelper.ScanAction.Idle));
            Assert.That(rateLimited.NextUpdate, Is.EqualTo(notDue));

            // Gone target -> Drop, timer untouched.
            var gone = ScannerUpdateHelper.Evaluate(transform, now, target, due, interval,
                null, coords, _ => true, _ => coords);
            Assert.That(gone.Action, Is.EqualTo(ScannerUpdateHelper.ScanAction.Drop));
            Assert.That(gone.NextUpdate, Is.EqualTo(due));

            // Infinite (null) range -> Push, timer advanced.
            var infinite = ScannerUpdateHelper.Evaluate(transform, now, target, due, interval,
                null, coords, _ => false, _ => coords);
            Assert.That(infinite.Action, Is.EqualTo(ScannerUpdateHelper.ScanAction.Push));
            Assert.That(infinite.NextUpdate, Is.EqualTo(advanced));

            // In range with a finite range (same coords) -> Push.
            var inRange = ScannerUpdateHelper.Evaluate(transform, now, target, due, interval,
                5f, coords, _ => false, _ => coords);
            Assert.That(inRange.Action, Is.EqualTo(ScannerUpdateHelper.ScanAction.Push));

            // Out of range -> Pause, timer still advanced (the paused state ships once).
            var outOfRange = ScannerUpdateHelper.Evaluate(transform, now, target, due, interval,
                5f, coords, _ => false, _ => farCoords);
            Assert.That(outOfRange.Action, Is.EqualTo(ScannerUpdateHelper.ScanAction.Pause));
            Assert.That(outOfRange.NextUpdate, Is.EqualTo(advanced));
        });

        await pair.CleanReturnAsync();
    }
}
