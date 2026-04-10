using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.HealthExaminable;
using Content.Shared.RussStation.Wounds;
using Content.Shared.RussStation.Wounds.Systems;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Shared.Traits.Assorted;

/// <summary>
/// Replaces the normal health examine verb with a numerical/technical readout
/// when the Self-Aware entity examines themselves.
/// </summary>
public sealed class SelfAwareSystem : EntitySystem
{
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedBloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly WoundDisplaySystem _woundDisplay = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SelfAwareComponent, GetVerbsEvent<ExamineVerb>>(
            OnGetExamineVerbs,
            after: new[] { typeof(HealthExaminableSystem) });
    }

    private void OnGetExamineVerbs(Entity<SelfAwareComponent> ent, ref GetVerbsEvent<ExamineVerb> args)
    {
        if (args.User != ent.Owner)
            return;

        if (!TryComp<DamageableComponent>(ent, out var damageable))
            return;

        if (!TryComp<HealthExaminableComponent>(ent, out var examinable))
            return;

        var healthVerbText = Loc.GetString("health-examinable-verb-text");
        ExamineVerb? existing = null;
        foreach (var v in args.Verbs)
        {
            if (v.Text == healthVerbText && v.Category == VerbCategory.Examine)
            {
                existing = v;
                break;
            }
        }
        if (existing != null)
            args.Verbs.Remove(existing);

        var user = args.User;
        var target = ent.Owner;

        var verb = new ExamineVerb
        {
            Act = () =>
            {
                var markup = CreateMarkup(target, examinable, damageable);
                _examine.SendExamineTooltip(user, target, markup, false, false);
            },
            Text = healthVerbText,
            Category = VerbCategory.Examine,
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/rejuvenate.svg.192dpi.png")),
        };

        args.Verbs.Add(verb);
    }

    private FormattedMessage CreateMarkup(EntityUid uid, HealthExaminableComponent examinable, DamageableComponent damage)
    {
        var msg = new FormattedMessage();
        var damageSpecifier = _damageable.GetAllDamage((uid, damage));
        var totalDamage = damageSpecifier.GetTotal();

        msg.AddMarkupOrThrow(Loc.GetString("self-aware-total-damage",
            ("amount", totalDamage.Int())));

        if (TryComp<BloodstreamComponent>(uid, out var bloodstream))
        {
            var bloodPercent = _bloodstream.GetBloodLevel((uid, bloodstream));
            msg.PushNewline();
            msg.AddMarkupOrThrow(Loc.GetString("self-aware-blood-level",
                ("percent", (bloodPercent * 100f).ToString("0"))));
        }

        var anyDamage = false;
        foreach (var type in examinable.ExaminableTypes)
        {
            if (!damageSpecifier.DamageDict.TryGetValue(type, out var dmg))
                continue;

            if (dmg == FixedPoint2.Zero)
                continue;

            if (!anyDamage)
            {
                msg.PushNewline();
                anyDamage = true;
            }
            msg.PushNewline();

            msg.AddMarkupOrThrow(Loc.GetString("self-aware-damage-type",
                ("type", type),
                ("amount", dmg.Int())));
        }

        var woundInfos = _woundDisplay.GetWoundDisplayInfo(uid);
        if (woundInfos.Count > 0)
        {
            msg.PushNewline();
            msg.PushNewline();
            msg.AddMarkupOrThrow(Loc.GetString("wound-examine-header"));
            foreach (var wound in woundInfos)
            {
                msg.PushNewline();
                msg.AddMarkupOrThrow(Loc.GetString("wound-examine-entry",
                    ("name", Loc.GetString(wound.LocKey)),
                    ("tier", wound.Tier)));
            }
        }

        return msg;
    }
}
