using Content.Shared.Body;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Light.Components;
using Content.Shared.Popups;
using Content.Shared.RussStation.Skillchips.Systems;

namespace Content.Shared.RussStation.Skillchips.Consumers;

/// <summary>
/// Burn-suppression for the N16H7M4R3 skillchip (<c>lightbulb_removing</c> capability).
/// Zeroes the heat damage <see cref="DamageOnInteractSystem"/> deals when a chip
/// holder grabs a lit bulb. We gate on the origin entity being a powered light so
/// other <c>DamageOnInteract</c> sources (hot produce, heat anomalies, strobe
/// lights) still burn as normal. Lives in shared so the client also zeroes the
/// predicted damage; otherwise client-side <c>DamageOnInteractSystem</c> still
/// predicts the burn popup and sound even though the server applies no damage.
/// SS13 parallel: TRAIT_LIGHTBULB_REMOVER on /obj/machinery/light handling.
/// </summary>
public sealed partial class LightbulbRemovingSystem : EntitySystem
{
    public const string LightbulbRemovingTag = "lightbulb_removing";

    [Dependency] private SharedSkillchipSystem _skillchip = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        // BodyComponent anchors the subscription on chip-eligible mobs. DamageModifyEvent
        // runs before the damage is applied, so zeroing args.Damage here also nukes the
        // popup / sound / paralyze branch in DamageOnInteractSystem (it only fires when
        // totalDamage.AnyPositive() after the return from ChangeDamage).
        SubscribeLocalEvent<BodyComponent, DamageModifyEvent>(OnDamageModify);
    }

    private void OnDamageModify(EntityUid mob, BodyComponent _, DamageModifyEvent args)
    {
        if (args.Origin is not { } origin || !HasComp<PoweredLightComponent>(origin))
            return;

        if (!_skillchip.HasCapability(mob, LightbulbRemovingTag))
            return;

        args.Damage = new DamageSpecifier();
        _popup.PopupClient(Loc.GetString("skillchip-lightbulb-removing-shrug"), mob, mob);
    }
}
