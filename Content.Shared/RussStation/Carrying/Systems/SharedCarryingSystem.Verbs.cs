using Content.Shared.Buckle.Components;
using Content.Shared.RussStation.Carrying.Components;
using Content.Shared.RussStation.Carrying.Events;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Shared.RussStation.Carrying.Systems;

// Verb providers for starting a carry, dropping it, and the third-party interrupt,
// plus the interrupt do-after that backs the latter.
public abstract partial class SharedCarryingSystem
{
    private void InitializeVerbs()
    {
        SubscribeLocalEvent<CarriableComponent, GetVerbsEvent<InteractionVerb>>(AddCarryVerb);
        SubscribeLocalEvent<BeingCarriedComponent, GetVerbsEvent<InteractionVerb>>(AddCarriedVerbs);

        SubscribeLocalEvent<BeingCarriedComponent, CarryInterruptDoAfterEvent>(OnCarryInterruptDoAfter);
    }

    private void AddCarryVerb(EntityUid uid, CarriableComponent component, GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (args.User == args.Target)
            return;

        if (!CanCarry(args.User, args.Target))
            return;

        args.Verbs.Add(new InteractionVerb
        {
            Text = Loc.GetString("carrying-verb-carry"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/pickup.svg.192dpi.png")),
            Act = () =>
            {
                if (TryComp<CarrierComponent>(args.User, out var carrier) && CanCarry(args.User, args.Target))
                    StartCarryDoAfter(args.User, args.Target, carrier);
            },
        });
    }

    private void AddCarriedVerbs(EntityUid uid, BeingCarriedComponent component, GetVerbsEvent<InteractionVerb> args)
    {
        if (args.User == component.Carrier)
        {
            args.Verbs.Add(new InteractionVerb
            {
                Text = Loc.GetString("carrying-verb-drop"),
                Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/drop.svg.192dpi.png")),
                Act = () => Drop(args.User),
            });
            return;
        }

        if (!args.CanAccess || !args.CanInteract)
            return;

        // Carried is the target itself; only third parties get the interrupt.
        if (args.User == uid)
            return;

        if (!TryComp<CarriableComponent>(uid, out var carriable))
            return;

        if (!CanInterruptCarry(args.User, carriable))
            return;

        args.Verbs.Add(new InteractionVerb
        {
            Text = Loc.GetString("carrying-verb-interrupt"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/drop.svg.192dpi.png")),
            Act = () => StartInterruptDoAfter(args.User, uid, carriable),
        });
    }

    private bool CanInterruptCarry(EntityUid user, CarriableComponent carriable)
    {
        if (_standing.IsDown(user) || _mobState.IsIncapacitated(user))
            return false;

        if (TryComp<BuckleComponent>(user, out var buckle) && buckle.Buckled)
            return false;

        if (carriable.InterruptRequiresFreeHand && _hands.CountFreeHands(user) < 1)
            return false;

        return true;
    }

    private void StartInterruptDoAfter(EntityUid user, EntityUid target, CarriableComponent carriable)
    {
        var doAfterArgs = new DoAfterArgs(EntityManager, user, carriable.InterruptDuration, new CarryInterruptDoAfterEvent(), target, target: target)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = carriable.InterruptRequiresFreeHand,
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void OnCarryInterruptDoAfter(EntityUid uid, BeingCarriedComponent component, CarryInterruptDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.User == uid || args.User == component.Carrier)
            return;

        args.Handled = true;
        InterruptCarry(args.User, uid);
    }

    /// <summary>
    /// Third-party interrupt completion path: ends the carry on
    /// <paramref name="target"/>, stuns the carrier, and plays popups.
    /// No-ops silently if the carry ended (or swapped carriers) between
    /// DoAfter start and completion. Public so tests can exercise the
    /// completion outcome without running the full DoAfter.
    /// </summary>
    public void InterruptCarry(EntityUid user, EntityUid target)
    {
        if (!TryComp<BeingCarriedComponent>(target, out var being))
            return;

        var carrier = being.Carrier;
        if (!HasComp<ActiveCarrierComponent>(carrier))
            return;

        if (TryComp<CarriableComponent>(target, out var carriable))
            _stun.TryUpdateStunDuration(carrier, carriable.InterruptStunDuration);

        Drop(carrier);

        _popup.PopupPredicted(
            Loc.GetString("carrying-interrupt-user", ("target", target), ("carrier", carrier)),
            Loc.GetString("carrying-interrupt-carrier", ("user", user), ("target", target)),
            user,
            user);
        _popup.PopupEntity(
            Loc.GetString("carrying-interrupt-carried", ("user", user), ("carrier", carrier)),
            target,
            target);
    }
}
