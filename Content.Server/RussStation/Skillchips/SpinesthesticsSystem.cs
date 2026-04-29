using Content.Server.Explosion.EntitySystems;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Popups;
using Content.Shared.RussStation.Skillchips;
using Robust.Shared.Random;

namespace Content.Server.RussStation.Skillchips;

/// <summary>
/// Tracks degradation of the Old F058UR7 skillchip.
/// Each Spin or Flip decrements the use counter; past certain thresholds,
/// side effects escalate from sparks/cellular damage up to a small explosion.
/// </summary>
public sealed class SpinesthesticsSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private static readonly HashSet<string> TriggerEmotes = ["Spin", "Flip"];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SpinesthesticsComponent, EmoteEvent>(OnEmote);
    }

    private void OnEmote(Entity<SpinesthesticsComponent> ent, ref EmoteEvent args)
    {
        if (!TriggerEmotes.Contains(args.Emote.ID))
            return;

        var comp = ent.Comp;
        comp.UsesRemaining--;

        if (comp.UsesRemaining <= 0)
        {
            _popup.PopupEntity(Loc.GetString("spinesthetics-critical"), ent, ent, PopupType.LargeCaution);
            _explosion.QueueExplosion(ent, "Default", totalIntensity: 5f, slope: 5f, maxTileIntensity: 2f,
                tileBreakScale: 0f, canCreateVacuum: false, user: ent);
            RemCompDeferred<SpinesthesticsComponent>(ent);
            return;
        }

        if (comp.UsesRemaining <= comp.ExplosionThreshold)
        {
            _popup.PopupEntity(Loc.GetString("spinesthetics-smoke"), ent, ent, PopupType.MediumCaution);
            ApplyDamage(ent, "Cellular", 20f);
            return;
        }

        if (comp.UsesRemaining <= comp.SmokeThreshold)
        {
            _popup.PopupEntity(Loc.GetString("spinesthetics-sparks"), ent, ent, PopupType.SmallCaution);
            ApplyDamage(ent, "Cellular", 8f);
            return;
        }

        if (comp.UsesRemaining <= comp.SparksThreshold)
        {
            _popup.PopupEntity(Loc.GetString("spinesthetics-warning"), ent, ent, PopupType.Small);
        }
    }

    private void ApplyDamage(EntityUid uid, string type, float amount)
    {
        var spec = new DamageSpecifier();
        spec.DamageDict.Add(type, amount);
        _damageable.TryChangeDamage(uid, spec, origin: uid);
    }
}
