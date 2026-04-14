using Content.IntegrationTests.Fixtures;
using Content.Server.RussStation.Economy;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Content.Shared.RussStation.Economy.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.RussStation.Economy;

[TestOf(typeof(PayrollSystem))]
public sealed class PayrollJobUpdateTest : GameTest
{
    /// <summary>
    /// Verifies that when a role is added mid-round via RoleAddedEvent,
    /// the PayrollSystem updates the PlayerBalanceComponent.JobId to the new job.
    /// This is the fix for #345: without this handler, wage tier was locked
    /// to whatever job the player spawned with.
    /// </summary>
    [Test]
    public async Task RoleAddedUpdatesJobIdTest()
    {
        var server = Server;

        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var mindSystem = entMan.System<SharedMindSystem>();
            var roleSystem = entMan.System<SharedRoleSystem>();

            // Create mob with balance component, initial job is Passenger.
            var mob = entMan.SpawnEntity(null, new MapCoordinates());
            var balance = entMan.AddComponent<PlayerBalanceComponent>(mob);
            balance.JobId = "Passenger";
            entMan.AddComponent<MindContainerComponent>(mob);

            // Create a mind and attach it to the mob.
            var mindId = mindSystem.CreateMind(null).Owner;
            mindSystem.TransferTo(mindId, mob);

            // Give the mind a job role. MindAddJobRole raises RoleAddedEvent,
            // which should trigger PayrollSystem.OnRoleAdded to update JobId.
            roleSystem.MindAddJobRole(mindId, jobPrototype: "StationEngineer");

            Assert.That(balance.JobId, Is.EqualTo("StationEngineer"),
                "JobId should be updated to the new role after RoleAddedEvent.");
        });
    }

    /// <summary>
    /// Verifies that RoleAddedEvent with no job role component (e.g., antag roles)
    /// does not clear the existing JobId.
    /// </summary>
    [Test]
    public async Task NonJobRoleDoesNotClearJobIdTest()
    {
        var server = Server;

        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var mindSystem = entMan.System<SharedMindSystem>();
            var roleSystem = entMan.System<SharedRoleSystem>();

            var mob = entMan.SpawnEntity(null, new MapCoordinates());
            var balance = entMan.AddComponent<PlayerBalanceComponent>(mob);
            balance.JobId = "StationEngineer";
            entMan.AddComponent<MindContainerComponent>(mob);

            var mindId = mindSystem.CreateMind(null).Owner;
            mindSystem.TransferTo(mindId, mob);

            // Add a non-job role (traitor). This should not affect JobId.
            roleSystem.MindAddRole(mindId, "MindRoleTraitor");

            Assert.That(balance.JobId, Is.EqualTo("StationEngineer"),
                "JobId should remain unchanged when a non-job role is added.");
        });
    }
}
