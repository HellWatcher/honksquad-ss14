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
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SabrageableComponent : Component
{
    /// <summary>
    /// Multiplier on the square root of weapon damage. Diminishing returns:
    /// a saber (17 damage) earns ~82 raw points, an energy sword (20) ~89.
    /// Combined with <see cref="BaseOffset"/> the bare-knuckle ceiling lands
    /// around the mid-50s.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float SuccessSqrtFactor = SabrageConstants.SuccessSqrtFactor;

    /// <summary>
    /// Flat offset on the no-chip term. Intentionally negative so a low-damage
    /// shard doesn't earn a free coin-flip. The pre-multiplier value is
    /// clamped to 0 before the chip multiplier runs, so this only shaves the
    /// low end.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float BaseOffset = SabrageConstants.BaseOffset;

    /// <summary>
    /// Multiplier applied to the success chance when the user has the
    /// <c>sabrage_proficiency</c> capability. The chip roughly doubles the
    /// odds at every weapon tier, with the final value clamped to 100.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float SkillchipMultiplier = SabrageConstants.SkillchipMultiplier;

    /// <summary>
    /// Minimum weapon damage required to attempt sabrage at all. SS13 gates on
    /// item force >= 5; matched here against <see cref="DamageSpecifier.GetTotal"/>.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MinimumDamage = SabrageConstants.MinimumDamage;

    /// <summary>How long the wind-up swing takes.</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan SwingDuration = TimeSpan.FromSeconds(SabrageConstants.SwingSeconds);

    /// <summary>Damage dealt to the user on a failed swing (cut yourself on the bottle).</summary>
    [DataField]
    public DamageSpecifier FailureDamage = new()
    {
        DamageDict = new() { { "Slash", 8 } },
    };

    /// <summary>What to leave behind on a failed swing. Matches the destructible
    /// drop on a bottle thrown hard enough to break.</summary>
    [DataField]
    public EntProtoId BrokenPrototype = "BrokenBottle";

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
