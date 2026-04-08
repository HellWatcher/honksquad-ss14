using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Shared.Traits.Assorted;

/// <summary>
/// Adds an examine verb showing exact damage numbers when examining yourself.
/// Requires <see cref="SelfAwareComponent"/>.
/// </summary>
public sealed class SelfAwareSystem : EntitySystem
{
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SelfAwareComponent, GetVerbsEvent<ExamineVerb>>(OnGetExamineVerbs);
    }

    private void OnGetExamineVerbs(Entity<SelfAwareComponent> ent, ref GetVerbsEvent<ExamineVerb> args)
    {
        if (args.User != ent.Owner)
            return;

        if (!TryComp<DamageableComponent>(ent, out var damageable))
            return;

        var user = args.User;
        var target = ent.Owner;

        var verb = new ExamineVerb
        {
            Act = () =>
            {
                var markup = CreateMarkup(target, damageable);
                _examine.SendExamineTooltip(user, target, markup, false, false);
            },
            Text = Loc.GetString("self-aware-examine-verb"),
            Category = VerbCategory.Examine,
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/rejuvenate.svg.192dpi.png")),
        };

        args.Verbs.Add(verb);
    }

    private FormattedMessage CreateMarkup(EntityUid uid, DamageableComponent damageable)
    {
        var msg = new FormattedMessage();
        var damage = _damageable.GetAllDamage((uid, damageable));
        var total = damage.GetTotal();

        if (total == FixedPoint2.Zero)
        {
            msg.AddMarkupOrThrow(Loc.GetString("self-aware-no-damage"));
            return msg;
        }

        msg.AddMarkupOrThrow(Loc.GetString("self-aware-damage-header"));

        var first = true;
        foreach (var (type, value) in damage.DamageDict)
        {
            if (value <= FixedPoint2.Zero)
                continue;

            if (!first)
                msg.PushNewline();
            first = false;

            msg.AddMarkupOrThrow(Loc.GetString("self-aware-damage-type",
                ("type", type),
                ("amount", value)));
        }

        return msg;
    }
}
