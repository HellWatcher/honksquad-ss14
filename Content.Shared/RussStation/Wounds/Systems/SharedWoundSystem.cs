using Content.Shared.Body.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.RussStation.Wounds.Systems;

public abstract class SharedWoundSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly List<WoundTypePrototype> _woundTypes = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WoundComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
        CacheWoundTypes();
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<WoundTypePrototype>())
            CacheWoundTypes();
    }

    private void CacheWoundTypes()
    {
        _woundTypes.Clear();
        foreach (var proto in _proto.EnumeratePrototypes<WoundTypePrototype>())
        {
            _woundTypes.Add(proto);
        }
    }

    private void OnDamageChanged(EntityUid uid, WoundComponent comp, DamageChangedEvent args)
    {
        if (_timing.ApplyingState)
            return;

        if (args.DamageDelta is null || !args.DamageIncreased)
            return;

        foreach (var (damageType, amount) in args.DamageDelta.DamageDict)
        {
            var amountFloat = amount.Float();
            if (amountFloat <= 0)
                continue;

            var typeStr = damageType;

            // Track bleed source damage type for display
            if (typeStr == "Slash" || typeStr == "Piercing")
            {
                UpdateBleedSource(comp, typeStr, amountFloat);
                continue; // Bleeding wounds are display-only, not wound entries
            }

            // Check spike thresholds for fracture/burn wound types
            foreach (var woundProto in _woundTypes)
            {
                if (woundProto.DamageType != typeStr)
                    continue;

                var tier = GetTierFromSpike(woundProto, amountFloat);
                if (tier <= 0)
                    continue;

                ApplyWound(comp, woundProto, tier);
            }
        }

        Dirty(uid, comp);
        RaiseLocalEvent(uid, new WoundsDamagedEvent());
    }

    private void UpdateBleedSource(WoundComponent comp, string damageType, float amount)
    {
        // If no current source, set it. Otherwise, prefer Slash on tie.
        if (comp.BleedSourceDamageType == null)
        {
            comp.BleedSourceDamageType = damageType;
            return;
        }

        // When both contribute equally, prefer Slash per spec
        if (damageType == "Slash")
            comp.BleedSourceDamageType = "Slash";
        else if (comp.BleedSourceDamageType != "Slash")
            comp.BleedSourceDamageType = damageType;
    }

    private static int GetTierFromSpike(WoundTypePrototype proto, float amount)
    {
        var tier = 0;
        for (var i = 0; i < proto.Thresholds.Length; i++)
        {
            if (amount >= proto.Thresholds[i])
                tier = i + 1;
        }
        return tier;
    }

    private static void ApplyWound(WoundComponent comp, WoundTypePrototype proto, int tier)
    {
        // Try to upgrade an existing wound of this type first
        foreach (var wound in comp.ActiveWounds)
        {
            if (wound.WoundTypeId != proto.ID)
                continue;

            if (wound.Tier < tier)
            {
                wound.Tier = tier;
                wound.TimeAtCurrentTier = TimeSpan.Zero;
                return;
            }

            if (wound.Tier < 3)
                return; // Existing wound is same or higher tier, no action

            // At tier 3, fall through to stack a new wound
            break;
        }

        // Create new wound entry
        comp.ActiveWounds.Add(new WoundEntry(proto.ID, tier, TimeSpan.Zero));
    }

    /// <summary>
    /// Removes all wounds of the given category. Used by surgery.
    /// </summary>
    public void ClearWoundsByCategory(EntityUid uid, WoundCategory category, WoundComponent? comp = null)
    {
        if (!Resolve(uid, ref comp, false))
            return;

        comp.ActiveWounds.RemoveAll(w =>
        {
            if (!_proto.TryIndex(w.WoundTypeId, out var proto))
                return true;
            return proto.Category == category;
        });

        Dirty(uid, comp);
    }

    /// <summary>
    /// Gets the highest tier among active wounds of a given category.
    /// Returns 0 if no wounds of that category exist.
    /// </summary>
    public int GetWorstTier(WoundComponent comp, WoundCategory category)
    {
        var worst = 0;
        foreach (var wound in comp.ActiveWounds)
        {
            if (!_proto.TryIndex(wound.WoundTypeId, out var proto))
                continue;

            if (proto.Category == category && wound.Tier > worst)
                worst = wound.Tier;
        }
        return worst;
    }

    /// <summary>
    /// Gets the bleeding tier based on BleedAmount thresholds.
    /// Returns 0 if not bleeding.
    /// </summary>
    public int GetBleedTier(WoundComponent woundComp, BloodstreamComponent bloodComp)
    {
        var bleed = bloodComp.BleedAmount;
        if (bleed <= 0)
            return 0;

        var tier = 0;
        for (var i = 0; i < woundComp.BleedTierThresholds.Length; i++)
        {
            if (bleed >= woundComp.BleedTierThresholds[i])
                tier = i + 1;
        }
        return tier;
    }

    /// <summary>
    /// Builds a list of wound display info for UI purposes.
    /// </summary>
    public List<WoundDisplayInfo> GetWoundDisplayInfo(EntityUid uid, WoundComponent? woundComp = null, BloodstreamComponent? bloodComp = null)
    {
        var result = new List<WoundDisplayInfo>();

        if (!Resolve(uid, ref woundComp, false))
            return result;

        // Bleeding wounds (derived from BleedAmount)
        if (Resolve(uid, ref bloodComp, false))
        {
            var bleedTier = GetBleedTier(woundComp, bloodComp);
            if (bleedTier > 0)
            {
                var source = woundComp.BleedSourceDamageType ?? "Slash";
                var locKey = $"wound-bleed-{source.ToLowerInvariant()}-{bleedTier}";
                result.Add(new WoundDisplayInfo(locKey, bleedTier, WoundCategory.Fracture));
            }
        }

        // Active wounds (fractures and burns)
        foreach (var wound in woundComp.ActiveWounds)
        {
            if (!_proto.TryIndex(wound.WoundTypeId, out var proto))
                continue;

            if (!proto.Names.TryGetValue(wound.Tier, out var name))
                continue;

            var locKey = $"wound-{proto.ID.ToLowerInvariant()}-{wound.Tier}";
            result.Add(new WoundDisplayInfo(locKey, wound.Tier, proto.Category));
        }

        // Sort by tier descending, then category
        result.Sort((a, b) =>
        {
            var tierCmp = b.Tier.CompareTo(a.Tier);
            return tierCmp != 0 ? tierCmp : a.Category.CompareTo(b.Category);
        });

        return result;
    }
}
