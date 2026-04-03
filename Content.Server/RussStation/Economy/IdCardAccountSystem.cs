using Content.Server.Administration;
using Content.Server.Popups;
using Content.Server.Stack;
using Content.Shared.Access.Components;
using Content.Shared.Database;
using Content.Shared.Popups;
using Content.Shared.Interaction;
using Content.Shared.RussStation.Economy.Components;
using Content.Shared.Stacks;
using Content.Shared.Verbs;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.RussStation.Economy;

/// <summary>
/// Handles bank account interactions on ID cards: set account, withdraw spesos, deposit spesos.
/// </summary>
public sealed class IdCardAccountSystem : EntitySystem
{
    [Dependency] private readonly QuickDialogSystem _quickDialog = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly PlayerBalanceSystem _balance = default!;
    [Dependency] private readonly StackSystem _stack = default!;

    private static readonly ProtoId<StackPrototype> CreditStack = "Credit";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IdCardComponent, GetVerbsEvent<ActivationVerb>>(OnGetVerbs);
        SubscribeLocalEvent<IdCardComponent, InteractUsingEvent>(OnInteractUsing);
    }

    private void OnGetVerbs(EntityUid uid, IdCardComponent comp, GetVerbsEvent<ActivationVerb> args)
    {
        if (!TryComp(args.User, out ActorComponent? actor))
            return;

        if (!args.CanInteract || !args.CanAccess)
            return;

        // Set Account verb.
        args.Verbs.Add(new ActivationVerb
        {
            Text = Loc.GetString("id-card-set-account-verb"),
            Act = () =>
            {
                _quickDialog.OpenDialog(actor.PlayerSession,
                    Loc.GetString("id-card-set-account-title"),
                    Loc.GetString("id-card-set-account-prompt"),
                    (string accountNumber) =>
                    {
                        OnAccountEntered(uid, args.User, actor.PlayerSession, accountNumber);
                    });
            },
            Impact = LogImpact.Low,
        });

        // Withdraw verb (only if the card has an account linked).
        if (!string.IsNullOrEmpty(comp.AccountNumber))
        {
            args.Verbs.Add(new ActivationVerb
            {
                Text = Loc.GetString("id-card-withdraw-verb"),
                Act = () =>
                {
                    _quickDialog.OpenDialog(actor.PlayerSession,
                        Loc.GetString("id-card-withdraw-title"),
                        Loc.GetString("id-card-withdraw-prompt"),
                        (int amount) =>
                        {
                            OnWithdraw(uid, args.User, actor.PlayerSession, amount);
                        });
                },
                Impact = LogImpact.Low,
            });
        }
    }

    /// <summary>
    /// Deposit spesos onto an ID card's linked account by using cash on the card.
    /// </summary>
    private void OnInteractUsing(EntityUid uid, IdCardComponent comp, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (string.IsNullOrEmpty(comp.AccountNumber))
            return;

        if (!TryComp<StackComponent>(args.Used, out var stack) || stack.StackTypeId != CreditStack)
            return;

        if (!_balance.TryGetByAccount(comp.AccountNumber, out var owner))
            return;

        var amount = stack.Count;
        if (amount <= 0)
            return;

        _balance.AddBalance(owner, amount);
        QueueDel(args.Used);

        _popup.PopupEntity(
            Loc.GetString("id-card-deposit-success", ("amount", amount)),
            uid,
            args.User);

        args.Handled = true;
    }

    private void OnAccountEntered(EntityUid idCard, EntityUid user, ICommonSession session, string accountNumber)
    {
        if (!HasComp<IdCardComponent>(idCard))
            return;

        accountNumber = accountNumber.Trim().ToUpperInvariant();

        if (string.IsNullOrEmpty(accountNumber))
            return;

        if (!_balance.TryGetByAccount(accountNumber, out _))
        {
            _popup.PopupEntity(Loc.GetString("id-card-set-account-invalid"), idCard, session, PopupType.MediumCaution);
            return;
        }

        var comp = Comp<IdCardComponent>(idCard);
        comp.AccountNumber = accountNumber;
        Dirty(idCard, comp);

        _popup.PopupEntity(Loc.GetString("id-card-set-account-success"), idCard, session, PopupType.Medium);
    }

    private void OnWithdraw(EntityUid idCard, EntityUid user, ICommonSession session, int amount)
    {
        if (!TryComp<IdCardComponent>(idCard, out var comp))
            return;

        if (string.IsNullOrEmpty(comp.AccountNumber))
            return;

        if (amount <= 0)
            return;

        if (!_balance.TryGetByAccount(comp.AccountNumber, out var owner))
            return;

        if (!_balance.TryDeduct(owner, amount))
        {
            _popup.PopupEntity(Loc.GetString("id-card-withdraw-insufficient"), idCard, session, PopupType.MediumCaution);
            return;
        }

        _stack.SpawnNextToOrDrop(amount, CreditStack, user);

        _popup.PopupEntity(
            Loc.GetString("id-card-withdraw-success", ("amount", amount)),
            idCard,
            session);
    }
}
