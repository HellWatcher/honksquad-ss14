using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Magic.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.RussStation.Weapons;

/// <summary>
/// Tests for the fork's spit action.
///
/// Upstream removed ActionGun in wizden #43539 (the space dragon fire-breath sound fix),
/// so spit was migrated from ActionGun + the fork's ActionGunExt workaround onto upstream's
/// ProjectileSpellEvent. The sound now comes from ActionComponent.Sound (played at the
/// performer) and the popup from PopupOnActionComponent, which is why the fork no longer
/// carries any spit-specific C#. These tests pin that wiring so a future upstream sync
/// can't quietly drop it.
/// </summary>
[TestOf(typeof(PopupOnActionComponent))]
public sealed class SpitActionTest : GameTest
{
    // Held as EntProtoId fields rather than inline strings: TryIndex/HasIndex forbid
    // literal ids (RA0033).
    private static readonly EntProtoId ActionSpit = "ActionSpit";
    private static readonly EntProtoId ProjectileSpit = "ProjectileSpit";
    // A CONCRETE descendant of BaseSpeciesMob, which carries the grant. The base itself is
    // abstract:true, and abstract prototypes are never put in the prototype index
    // (PrototypeManager.TryReadPrototype returns null for them), so TryIndex always fails
    // on it. Going through a spawnable mob also proves the grant survives inheritance.
    private static readonly EntProtoId HumanMob = "MobHuman";

    /// <summary>
    /// ActionSpit must fire a ProjectileSpellEvent carrying the spit projectile, and must
    /// keep the sound, popup and cooldown that used to live on the deleted SpitGun entity.
    /// </summary>
    [Test]
    public async Task SpitActionIsWiredToProjectileSpell()
    {
        var server = Server;
        var protoManager = server.ResolveDependency<IPrototypeManager>();
        var compFactory = server.ResolveDependency<IComponentFactory>();

        await server.WaitAssertion(() =>
        {
            Assert.That(protoManager.TryIndex(ActionSpit, out var action), Is.True,
                "ActionSpit prototype should exist");
            Assert.That(protoManager.HasIndex(ProjectileSpit), Is.True,
                "ProjectileSpit prototype should exist");

            // No SpitGun assertion: the YAML linter validates every EntProtoId field, so a
            // field naming a deliberately-deleted prototype fails validation. The assertions
            // below already pin that spit runs through ProjectileSpell rather than a gun.

            Assert.That(action!.TryGetComponent<WorldTargetActionComponent>(out var worldTarget, compFactory), Is.True,
                "ActionSpit should be a world-target action");
            Assert.That(worldTarget!.Event, Is.TypeOf<ProjectileSpellEvent>(),
                "ActionSpit should raise a ProjectileSpellEvent");

            var spell = (ProjectileSpellEvent) worldTarget.Event!;
            Assert.That(spell.Prototype.Id, Is.EqualTo("ProjectileSpit"));
            Assert.That(spell.ProjectileSpeed, Is.EqualTo(10f),
                "Projectile speed should match the old SpitGun's projectileSpeed");

            Assert.That(action.TryGetComponent<ActionComponent>(out var actionComp, compFactory), Is.True);
            Assert.That(actionComp!.Sound, Is.Not.Null,
                "Spit sound must live on the action so it plays from the performer (wizden #43539)");
            Assert.That(actionComp.UseDelay, Is.EqualTo(TimeSpan.FromSeconds(0.5)),
                "UseDelay replaces the old gun's fire-rate throttle");

            Assert.That(action.TryGetComponent<PopupOnActionComponent>(out var popup, compFactory), Is.True,
                "Spit popup should come from upstream's PopupOnAction");
            Assert.That(popup!.SelfMessage, Is.EqualTo("action-spit-popup"));
            Assert.That(popup.OthersMessage, Is.EqualTo("action-spit-popup"));
        });
    }

    /// <summary>
    /// Every humanoid should be granted the spit action directly, now that there is no
    /// per-humanoid SpitGun entity being spawned into nullspace at map init.
    ///
    /// Asserted against the concrete mob rather than an appearance prototype. The grant sits
    /// on BaseSpeciesMob, and appearance prototypes deliberately do not carry it, since they
    /// double as the lobby preview dolls.
    /// </summary>
    [Test]
    public async Task HumanoidsAreGrantedSpit()
    {
        var server = Server;
        var protoManager = server.ResolveDependency<IPrototypeManager>();
        var compFactory = server.ResolveDependency<IComponentFactory>();

        await server.WaitAssertion(() =>
        {
            Assert.That(protoManager.TryIndex(HumanMob, out var mob), Is.True,
                "MobHuman prototype should exist");
            Assert.That(mob!.TryGetComponent<ActionGrantComponent>(out var grant, compFactory), Is.True,
                "Humanoids should grant actions directly");
            Assert.That(grant!.Actions.Select(a => a.Id), Does.Contain("ActionSpit"),
                "Humanoids should be granted ActionSpit");
        });
    }

    /// <summary>
    /// A spawned humanoid should end up with exactly one spit action, not several. The grant
    /// list being correct is not enough on its own: if more than one prototype in the chain
    /// carries an ActionGrant naming ActionSpit, or something grants it a second time at
    /// runtime, the player gets duplicate entries in their action bar.
    /// </summary>
    [Test]
    public async Task HumanoidGetsExactlyOneSpitAction()
    {
        await Pair.CreateTestMap();
        var mob = await SpawnAtPosition("MobHuman", Pair.TestMap!.GridCoords);
        await Pair.RunUntilSynced();

        var actionsSystem = Server.System<SharedActionsSystem>();

        await Server.WaitAssertion(() =>
        {
            var spitCount = actionsSystem
                .GetActions(mob)
                .Count(a => SEntMan.GetComponent<MetaDataComponent>(a.Owner).EntityPrototype?.ID == ActionSpit.Id);

            Assert.That(spitCount, Is.EqualTo(1),
                $"MobHuman should be granted ActionSpit exactly once, got {spitCount}");
        });
    }
}
