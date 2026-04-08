using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.RussStation.Wounds.Systems;

public abstract class SharedWoundSystem : EntitySystem
{
    [Dependency] protected readonly IPrototypeManager _proto = default!;
    [Dependency] protected readonly IGameTiming _timing = default!;
    [Dependency] private readonly WoundDisplaySystem _display = default!;

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

        var changed = false;

        foreach (var (damageType, amount) in args.DamageDelta.DamageDict)
        {
            var amountFloat = amount.Float();
            if (amountFloat <= 0)
                continue;

            var typeStr = damageType;

            // Track bleed source damage type for display
            if (typeStr == "Slash" || typeStr == "Piercing")
            {
                changed |= _display.UpdateBleedSource(comp, typeStr, amountFloat);
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

                changed |= ApplyWound(comp, woundProto, tier);
            }
        }

        if (!changed)
            return;

        Dirty(uid, comp);
        RaiseLocalEvent(uid, new WoundsDamagedEvent());
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

    private static bool ApplyWound(WoundComponent comp, WoundTypePrototype proto, int tier)
    {
        // Try to upgrade an existing wound of this type first
        var existingCount = 0;
        foreach (var wound in comp.ActiveWounds)
        {
            if (wound.WoundTypeId != proto.ID)
                continue;

            existingCount++;

            if (wound.Tier < tier)
            {
                wound.Tier = tier;
                wound.TimeAtCurrentTier = TimeSpan.Zero;
                return true;
            }

            if (wound.Tier < 3)
                return false; // Existing wound is same or higher tier, no action

            // At tier 3, fall through to stack a new wound
            break;
        }

        // Cap stacking at 3 wounds per type
        if (existingCount >= 3)
            return false;

        // Create new wound entry
        comp.ActiveWounds.Add(new WoundEntry(proto.ID, tier, TimeSpan.Zero));
        return true;
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

}
