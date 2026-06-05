using System.Collections.Generic;
using Content.Shared.Body;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Drunk;
using Content.Shared.FixedPoint;
using Content.Shared.RussStation.Skillchips;
using Content.Shared.RussStation.Skillchips.Consumers;
using Content.Shared.RussStation.Skillchips.Systems;
using Content.Shared.StatusEffectNew;
using Content.Shared.Weapons.Melee.Components;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.RussStation.Skillchips;

/// <summary>
/// Verifies <see cref="SharedDrunkenBrawlerSystem"/>'s offense branch: while a chip holder is
/// intoxicated, their unarmed swings hit harder. A sober holder (capability present but not drunk)
/// deals baseline damage.
/// </summary>
[TestFixture]
[TestOf(typeof(SharedDrunkenBrawlerSystem))]
public sealed class DrunkenBrawlerTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: skillchip
  id: DrunkenBrawlerTestChip
  name: Drunken Brawler Test Chip
  capacityCost: 1
  grants:
  - !type:CapabilityTagGrant
    tag: drunken_brawler

- type: entity
  id: DrunkenBrawlerTestBrain
  components:
  - type: Brain
  - type: Organ
    category: Brain
  - type: SkillchipHolder

- type: entity
  id: DrunkenBrawlerTestBody
  components:
  - type: Body
  - type: MobState
    allowedStates:
    - Alive
    - Critical
    - Dead
  - type: MobThresholds
    thresholds:
      0: Alive
      100: Critical
      200: Dead
  - type: Damageable
  - type: MeleeWeapon
    damage:
      types:
        Blunt: 5
";

    // Referenced via a named constant rather than an inline literal: RA0033 forbids string
    // literals in IPrototypeManager.Index.
    private const string BluntDamageType = "Blunt";

    [Test]
    public async Task DrunkBrawlerHitsHarderThanSoberTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var em = server.ResolveDependency<IEntityManager>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var skillchips = em.System<SharedSkillchipSystem>();
        var containers = em.System<SharedContainerSystem>();
        var status = em.System<StatusEffectsSystem>();
        var mapData = await pair.CreateTestMap();

        var blunt = protoMan.Index<DamageTypePrototype>(BluntDamageType);

        await server.WaitAssertion(() =>
        {
            var brain = em.SpawnEntity("DrunkenBrawlerTestBrain", mapData.GridCoords);
            var mob = em.SpawnEntity("DrunkenBrawlerTestBody", mapData.GridCoords);
            var holder = em.GetComponent<SkillchipHolderComponent>(brain);

            skillchips.TryInstall((brain, holder), "DrunkenBrawlerTestChip");
            var container = containers.EnsureContainer<Container>(mob, BodyComponent.ContainerID);
            containers.Insert(brain, container);

            Assert.That(skillchips.HasCapability(mob, SharedDrunkenBrawlerSystem.DrunkenBrawlerTag), Is.True);

            // Sober: an unarmed swing (User == Weapon == mob) deals baseline damage.
            var soberDamage = new DamageSpecifier(blunt, FixedPoint2.New(10));
            var soberEv = new GetMeleeDamageEvent(mob, soberDamage, new List<DamageModifierSet>(), mob);
            em.EventBus.RaiseLocalEvent(mob, ref soberEv);
            Assert.That(soberEv.Damage.GetTotal(), Is.EqualTo(FixedPoint2.New(10)),
                "A sober brawler should deal unmodified unarmed damage.");

            // Drunk: the same swing should hit harder.
            Assert.That(status.TryAddStatusEffectDuration(mob, SharedDrunkSystem.Drunk, TimeSpan.FromSeconds(60)), Is.True);

            var drunkDamage = new DamageSpecifier(blunt, FixedPoint2.New(10));
            var drunkEv = new GetMeleeDamageEvent(mob, drunkDamage, new List<DamageModifierSet>(), mob);
            em.EventBus.RaiseLocalEvent(mob, ref drunkEv);
            Assert.That(drunkEv.Damage.GetTotal(), Is.GreaterThan(FixedPoint2.New(10)),
                "A drunken brawler's unarmed swing should be boosted.");
        });

        await pair.CleanReturnAsync();
    }
}
