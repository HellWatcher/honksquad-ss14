using Content.Server.RussStation.Skillchips;
using Content.Shared.Chat;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Damage.Components;
using Content.Shared.Projectiles;
using Content.Shared.RussStation.Skillchips;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.RussStation.Skillchips;

/// <summary>
/// Verifies <see cref="BulletDodgeSystem"/>: a trigger emote (Flip) opens a deflect window,
/// an incoming projectile-reflect attempt during the window is cancelled and drains stamina,
/// and reflect attempts outside the window do nothing.
/// </summary>
[TestFixture]
[TestOf(typeof(BulletDodgeSystem))]
public sealed class BulletDodgeTest
{
    private const string FlipEmote = "Flip";

    [Test]
    public async Task TriggerEmoteOpensWindowTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var mob = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var comp = entMan.AddComponent<BulletDodgeComponent>(mob);

            Assert.That(comp.ActiveUntil, Is.Null, "Window should start closed.");

            var ev = new EmoteEvent(protoMan.Index<EmotePrototype>(FlipEmote));
            entMan.EventBus.RaiseLocalEvent(mob, ref ev);

            Assert.That(comp.ActiveUntil, Is.Not.Null, "A trigger emote should open the deflect window.");

            entMan.DeleteEntity(mob);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ReflectCancelledWhileWindowActiveTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var timing = server.ResolveDependency<IGameTiming>();

        await server.WaitAssertion(() =>
        {
            var mob = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var comp = entMan.AddComponent<BulletDodgeComponent>(mob);
            comp.ActiveUntil = timing.CurTime + TimeSpan.FromSeconds(1);

            var proj = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var projComp = entMan.AddComponent<ProjectileComponent>(proj);

            var ev = new ProjectileReflectAttemptEvent(proj, projComp, false);
            entMan.EventBus.RaiseLocalEvent(mob, ref ev);

            Assert.That(ev.Cancelled, Is.True, "Projectile should be deflected while the window is open.");
            Assert.That(comp.ActiveUntil, Is.Null, "The window should close immediately after a deflect.");

            entMan.DeleteEntity(mob);
            entMan.DeleteEntity(proj);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DeflectConsumesStaminaTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var timing = server.ResolveDependency<IGameTiming>();

        await server.WaitAssertion(() =>
        {
            var mob = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var comp = entMan.AddComponent<BulletDodgeComponent>(mob);
            var stamina = entMan.AddComponent<StaminaComponent>(mob);
            comp.ActiveUntil = timing.CurTime + TimeSpan.FromSeconds(1);

            var proj = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var projComp = entMan.AddComponent<ProjectileComponent>(proj);

            var ev = new ProjectileReflectAttemptEvent(proj, projComp, false);
            entMan.EventBus.RaiseLocalEvent(mob, ref ev);

            Assert.That(stamina.StaminaDamage, Is.GreaterThan(0f),
                "A successful deflect should drain the dodger's stamina.");

            entMan.DeleteEntity(mob);
            entMan.DeleteEntity(proj);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ReflectIgnoredWhenWindowClosedTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var mob = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            entMan.AddComponent<BulletDodgeComponent>(mob); // ActiveUntil stays null

            var proj = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var projComp = entMan.AddComponent<ProjectileComponent>(proj);

            var ev = new ProjectileReflectAttemptEvent(proj, projComp, false);
            entMan.EventBus.RaiseLocalEvent(mob, ref ev);

            Assert.That(ev.Cancelled, Is.False, "No deflect should happen when the window is closed.");

            entMan.DeleteEntity(mob);
            entMan.DeleteEntity(proj);
        });

        await pair.CleanReturnAsync();
    }
}
