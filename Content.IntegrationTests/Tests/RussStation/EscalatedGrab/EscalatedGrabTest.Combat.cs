using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.RussStation.EscalatedGrab;
using Content.Shared.RussStation.EscalatedGrab.Components;
using Content.Shared.RussStation.EscalatedGrab.Events;
using Content.Shared.RussStation.EscalatedGrab.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.RussStation.EscalatedGrab;

/// <summary>
/// Combat-side scenarios for the escalated grab system: the choke stamina drain,
/// damage-driven stage drops, and resist do-afters (including the Pushover quirk).
/// Shares the <c>Prototypes</c> fixture declared in <c>EscalatedGrabTest.cs</c>.
/// </summary>
public sealed partial class EscalatedGrabTest
{
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
