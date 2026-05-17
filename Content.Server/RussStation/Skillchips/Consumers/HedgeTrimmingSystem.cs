using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Kitchen.Components;
using Content.Shared.Popups;
using Content.Shared.RussStation.Skillchips.Consumers;
using Content.Shared.RussStation.Skillchips.Systems;
using Robust.Shared.Random;

namespace Content.Server.RussStation.Skillchips.Consumers;

/// <summary>
/// Server consumer for the <c>hedgetrimming</c> capability (Hedge 3 chip).
/// SS13's TRAIT_BONSAI lets a holder reshape a potted plant with any sharp
/// item over a short do-after (<c>kirbyplants.dm</c> attackby). Here: sharp
/// item plus the capability, three second trim, then the plant's sprite
/// re-rolls (networked via <see cref="HedgeTrimmableComponent"/>, applied
/// client-side).
/// </summary>
public sealed class HedgeTrimmingSystem : EntitySystem
{
    public const string HedgetrimmingTag = "hedgetrimming";

    [Dependency] private readonly SharedSkillchipSystem _skillchip = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HedgeTrimmableComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<HedgeTrimmableComponent, HedgeTrimDoAfterEvent>(OnTrimmed);
    }

    private void OnInteractUsing(Entity<HedgeTrimmableComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!HasComp<SharpComponent>(args.Used))
            return;

        if (!_skillchip.HasCapability(args.User, HedgetrimmingTag))
            return;

        var doAfter = new DoAfterArgs(
            EntityManager,
            args.User,
            ent.Comp.TrimDuration,
            new HedgeTrimDoAfterEvent(),
            ent.Owner,
            target: ent.Owner,
            used: args.Used)
        {
            BreakOnMove = true,
            NeedHand = true,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
        {
            _popup.PopupEntity(Loc.GetString("skillchip-hedgetrimming-start"), ent.Owner, args.User);
            args.Handled = true;
        }
    }

    private void OnTrimmed(Entity<HedgeTrimmableComponent> ent, ref HedgeTrimDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || ent.Comp.States.Count == 0)
            return;

        ent.Comp.CurrentState = _random.Pick(ent.Comp.States);
        Dirty(ent);

        _popup.PopupEntity(Loc.GetString("skillchip-hedgetrimming-finish"), ent.Owner, args.User);
        args.Handled = true;
    }
}
