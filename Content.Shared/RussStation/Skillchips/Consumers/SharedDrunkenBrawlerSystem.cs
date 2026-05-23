using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Drunk;
using Content.Shared.RussStation.Skillchips.Systems;
using Content.Shared.StatusEffectNew;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Components;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Shared.RussStation.Skillchips.Consumers;

/// <summary>
/// Combat handler for the F0RC3 4DD1CT10N skillchip
/// (<c>drunken_brawler</c> capability). While the carrier is intoxicated,
/// their unarmed swings hit harder and their incoming unarmed damage is
/// reduced. Mirrors SS13's <c>TRAIT_DRUNKEN_BRAWLER</c> branches in
/// <c>_species.dm</c>; the SS13 offense bonus continuously scales with
/// damage taken, but we settle for a flat multiplier so we can stay off
/// the obsolete <c>GetTotalDamage</c> API. The defense reduction stands
/// in for <c>armor_block += 20</c>.
///
/// Anchored on <see cref="MeleeWeaponComponent"/> because every
/// <c>MobCombat</c> mob carries one, so a single subscription covers both
/// "the user about to swing" and "the target about to be hit". Lives in
/// shared so the client predicts the same damage figures as the server.
/// </summary>
public sealed class SharedDrunkenBrawlerSystem : EntitySystem
{
    public const string DrunkenBrawlerTag = "drunken_brawler";

    /// <summary>Flat multiplier on unarmed damage dealt while drunk. SS13's floor was 1.3x.</summary>
    private const float OffenseMultiplier = 1.3f;

    /// <summary>
    /// Incoming-damage coefficient applied to drunken-brawler defenders. SS13 adds 20 flat
    /// armor on a 100-scale; the equivalent here is a 20% reduction on the incoming hit.
    /// </summary>
    private const float DefenseMultiplier = 0.8f;

    [Dependency] private readonly SharedSkillchipSystem _skillchip = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!;
    [Dependency] private readonly SharedMeleeWeaponSystem _melee = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MeleeWeaponComponent, GetMeleeDamageEvent>(OnGetMeleeDamage);
        SubscribeLocalEvent<MeleeWeaponComponent, DamageModifyEvent>(OnDamageModify);
    }

    private void OnGetMeleeDamage(Entity<MeleeWeaponComponent> ent, ref GetMeleeDamageEvent args)
    {
        // Held-weapon swings raise the event on the weapon entity, not the user. Unarmed swings
        // resolve to the mob's own MeleeWeaponComponent, so user == weapon means unarmed.
        if (args.User != args.Weapon)
            return;

        if (!IsDrunkenBrawler(args.User))
            return;

        args.Damage *= OffenseMultiplier;
    }

    private void OnDamageModify(Entity<MeleeWeaponComponent> ent, ref DamageModifyEvent args)
    {
        if (args.Origin is not { } attacker)
            return;

        if (!IsAttackerUnarmed(attacker))
            return;

        if (!IsDrunkenBrawler(ent.Owner))
            return;

        args.Damage *= DefenseMultiplier;
    }

    private bool IsDrunkenBrawler(EntityUid mob)
    {
        return _status.HasStatusEffect(mob, SharedDrunkSystem.Drunk)
               && _skillchip.HasCapability(mob, DrunkenBrawlerTag);
    }

    private bool IsAttackerUnarmed(EntityUid attacker)
    {
        // DamageModifyEvent.Origin is always the mob (TryChangeDamage passes user, not weapon),
        // so resolve the active weapon and check whether it's the mob itself.
        return _melee.TryGetWeapon(attacker, out var weapon, out _) && weapon == attacker;
    }
}
