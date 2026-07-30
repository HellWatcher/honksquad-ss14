namespace Content.IntegrationTests;

[SetUpFixture]
public sealed class PoolManagerTestEventHandler
{
    // HONK START - upstream's 20 minutes is sized for upstream's suite alone. The fork adds
    // ~258 of the 814 test methods on top of it, and after the v283.1.3 sync the combined run
    // no longer fits: 4105 of 5731 tests completed in 20m09s before this watchdog fired and
    // called Shutdown(), failing the remaining 1625 with "Pool manager has not been
    // initialized". Extrapolating that rate the full suite needs ~28 minutes, so 35 leaves
    // headroom without hiding a genuine hang.
    // The job's timeout-minutes in build-test-debug.yml must stay above HardStopTimeLimit.
    private static TimeSpan MaximumTotalTestingTimeLimit => TimeSpan.FromMinutes(35);
    // HONK END
    private static TimeSpan HardStopTimeLimit => MaximumTotalTestingTimeLimit.Add(TimeSpan.FromMinutes(1));

    [OneTimeSetUp]
    public void Setup()
    {
        PoolManager.Startup();
        // If the tests seem to be stuck, we try to end it semi-nicely
        _ = Task.Delay(MaximumTotalTestingTimeLimit).ContinueWith(_ =>
        {
            // This can and probably will cause server/client pairs to shut down MID test, and will lead to really confusing test failures.
            TestContext.Error.WriteLine($"\n\n{nameof(PoolManagerTestEventHandler)}: ERROR: Tests are taking too long. Shutting down all tests. This may lead to weird failures/exceptions.\n\n");
            PoolManager.Shutdown();
        });

        // If ending it nicely doesn't work within a minute, we do something a bit meaner.
        _ = Task.Delay(HardStopTimeLimit).ContinueWith(_ =>
        {
            var deathReport = PoolManager.DeathReport();
            Environment.FailFast($"Tests took way too ;\n Death Report:\n{deathReport}");
        });
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        PoolManager.Shutdown();
    }
}
