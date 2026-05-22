using Content.Shared.DoAfter;
using Content.Shared.Damage;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.RussStation.Skillchips.Consumers;

/// <summary>
/// Marks a bottle that can be sabraged: struck with a sharp item to pop the
/// cork off in one swing. The Le S48R4G3 skillchip (<c>sabrage_proficiency</c>
/// capability) bumps the success chance massively. SS13 parallel:
/// <c>/obj/item/reagent_containers/cup/glass/bottle/champagne</c> with its
/// <c>sabrage_success_percentile</c> field and <c>TRAIT_SABRAGE_PRO</c> check.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SabrageableComponent : Component
{
    /// <summary>
    /// Per-point-of-weapon-damage success chance, in percent. SS13 default is 5.
    /// A 15-damage saber against a default bottle yields a 75% base chance, plus
    /// any chip bonus.
    /// </summary>
    [DataField]
    public float SuccessPerDamage = 5f;

    /// <summary>
    /// Flat bonus added when the user has the <c>sabrage_proficiency</c>
    /// capability. SS13 uses 35.
    /// </summary>
    [DataField]
    public float SkillchipBonus = 35f;

    /// <summary>
    /// Minimum weapon damage required to attempt sabrage at all. SS13 gates on
    /// item force >= 5; matched here against <see cref="DamageSpecifier.GetTotal"/>.
    /// </summary>
    [DataField]
    public float MinimumDamage = 5f;

    /// <summary>How long the wind-up swing takes.</summary>
    [DataField]
    public TimeSpan SwingDuration = TimeSpan.FromSeconds(2);

    /// <summary>Damage dealt to the user on a failed swing (cut yourself on the bottle).</summary>
    [DataField]
    public DamageSpecifier FailureDamage = new()
    {
        DamageDict = new() { { "Slash", 8 } },
    };

    /// <summary>Glass shard prototype to spawn on a failed swing.</summary>
    [DataField]
    public EntProtoId ShardPrototype = "ShardGlass";

    /// <summary>How many shards to spawn on a failed swing.</summary>
    [DataField]
    public int ShardCount = 2;

    /// <summary>Sound that plays on the wind-up.</summary>
    [DataField]
    public string SwingSound = "/Audio/Items/unsheath.ogg";

    /// <summary>Sound that plays on a successful sabrage.</summary>
    [DataField]
    public string SuccessSound = "/Audio/Weapons/bladeslice.ogg";

    /// <summary>Sound that plays on a failed sabrage (bottle smash).</summary>
    [DataField]
    public string FailureSound = "/Audio/Effects/glass_break1.ogg";
}

[Serializable, NetSerializable]
public sealed partial class SabrageDoAfterEvent : SimpleDoAfterEvent;
