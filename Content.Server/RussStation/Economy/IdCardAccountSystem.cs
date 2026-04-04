using Content.Server.Administration;
using Content.Server.Popups;
using Content.Server.Stack;
using Content.Shared.Access.Components;
using Content.Shared.Database;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Hands;
using Content.Shared.Interaction;
using Content.Shared.PDA;
using Content.Shared.Popups;
using Content.Shared.RussStation.Economy.Components;
using Content.Shared.Stacks;
using Content.Shared.Verbs;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.RussStation.Economy;

/// <summary>
/// Handles bank account interactions on ID cards: set account, create account, withdraw, deposit.
/// Alt-click on an ID: prompts to set account (if none) or withdraw (if linked).
/// Right-click on an ID: create a new account (if none linked).
/// Use spesos on an ID or PDA (with ID inside) to deposit.
/// </summary>
public sealed class IdCardAccountSystem : EntitySystem
{
    [Dependency] private readonly QuickDialogSystem _quickDialog = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly PlayerBalanceSystem _balance = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    private static readonly ProtoId<StackPrototype> CreditStack = "Credit";

    /// <summary>
    /// Tracks which sessions have an open ID-card dialog.
    /// Prevents stacking dialogs on repeated alt-click and allows cancel on drop.
    /// </summary>
    private readonly HashSet<ICommonSession> _pendingDialogSessions = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IdCardComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAltVerbs);
        SubscribeLocalEvent<IdCardComponent, GetVerbsEvent<ActivationVerb>>(OnGetActivationVerbs);
        SubscribeLocalEvent<IdCardComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<IdCardComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<PdaComponent, InteractUsingEvent>(OnPdaInteractUsing);
        SubscribeLocalEvent<IdCardComponent, GotUnequippedHandEvent>(OnIdDropped);
    }

    private void OnIdDropped(EntityUid uid, IdCardComponent comp, GotUnequippedHandEvent args)
    {
        if (!TryComp(args.User, out ActorComponent? actor))
            return;

        if (!_pendingDialogSessions.Remove(actor.PlayerSession))
            return;

        _quickDialog.CloseAllDialogs(actor.PlayerSession);
    }

    private void OnGetAltVerbs(EntityUid uid, IdCardComponent comp, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!TryComp(args.User, out ActorComponent? actor))
            return;

        if (!args.CanInteract || !args.CanAccess)
            return;

        if (!_hands.IsHolding(args.User, uid))
            return;

        if (string.IsNullOrEmpty(comp.AccountNumber))
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("id-card-set-account-verb"),
                Act = () =>
                {
                    if (!_pendingDialogSessions.Add(actor.PlayerSession))
                        return;

                    _quickDialog.OpenDialog(actor.PlayerSession,
                        Loc.GetString("id-card-set-account-title"),
                        Loc.GetString("id-card-set-account-prompt"),
                        (string accountNumber) =>
                        {
                            _pendingDialogSessions.Remove(actor.PlayerSession);
                            OnAccountEntered(uid, args.User, actor.PlayerSession, accountNumber);
                        },
                        () => _pendingDialogSessions.Remove(actor.PlayerSession));
                },
                Impact = LogImpact.Low,
            });
        }
        else if (_balance.TryGetByAccount(comp.AccountNumber, out _))
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("id-card-withdraw-verb"),
                Act = () =>
                {
                    if (!_pendingDialogSessions.Add(actor.PlayerSession))
                        return;

                    _quickDialog.OpenDialog(actor.PlayerSession,
                        Loc.GetString("id-card-withdraw-title"),
                        Loc.GetString("id-card-withdraw-prompt"),
                        (int amount) =>
                        {
                            _pendingDialogSessions.Remove(actor.PlayerSession);
                            OnWithdraw(uid, args.User, actor.PlayerSession, amount);
                        },
                        () => _pendingDialogSessions.Remove(actor.PlayerSession));
                },
                Impact = LogImpact.Low,
            });
        }
    }

    private void OnGetActivationVerbs(EntityUid uid, IdCardComponent comp, GetVerbsEvent<ActivationVerb> args)
    {
        if (!TryComp(args.User, out ActorComponent? actor))
            return;

        if (!args.CanInteract || !args.CanAccess)
            return;

        if (!_hands.IsHolding(args.User, uid))
            return;

        if (!string.IsNullOrEmpty(comp.AccountNumber))
            return;

        args.Verbs.Add(new ActivationVerb
        {
            Text = Loc.GetString("id-card-create-account-verb"),
            Act = () =>
            {
                if (!_pendingDialogSessions.Add(actor.PlayerSession))
                    return;

                _quickDialog.OpenDialog(actor.PlayerSession,
                    Loc.GetString("id-card-create-account-title"),
                    Loc.GetString("id-card-create-account-confirm"),
                    (string confirmation) =>
                    {
                        _pendingDialogSessions.Remove(actor.PlayerSession);
                        if (confirmation.Trim().Equals("YES", StringComparison.OrdinalIgnoreCase))
                            OnCreateAccount(uid, args.User, actor.PlayerSession);
                    },
                    () => _pendingDialogSessions.Remove(actor.PlayerSession));
            },
            Impact = LogImpact.Low,
        });
    }

    private void OnExamine(EntityUid uid, IdCardComponent comp, ExaminedEvent args)
    {
        if (string.IsNullOrEmpty(comp.AccountNumber))
            return;

        using (args.PushGroup(nameof(IdCardAccountSystem)))
        {
            if (!_balance.TryGetByAccount(comp.AccountNumber, out var owner)
                || !TryComp<PlayerBalanceComponent>(owner, out var balanceComp))
            {
                args.PushMarkup(Loc.GetString("id-card-account-invalid-examine"));
                return;
            }

            args.PushMarkup(Loc.GetString("id-card-examine-balance", ("balance", balanceComp.Balance)));
        }
    }

    /// <summary>
    /// Deposit spesos onto an ID card's linked account by using cash on the card.
    /// </summary>
    private void OnInteractUsing(EntityUid uid, IdCardComponent comp, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = TryDeposit(uid, comp, args.Used, args.User);
    }

    /// <summary>
    /// Deposit spesos by using cash on a PDA. Passes through to the ID inside.
    /// </summary>
    private void OnPdaInteractUsing(EntityUid uid, PdaComponent pda, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<StackComponent>(args.Used, out var stack) || stack.StackTypeId != CreditStack)
            return;

        var idEntity = pda.IdSlot.Item;
        if (idEntity == null || !TryComp<IdCardComponent>(idEntity, out var idComp))
            return;

        args.Handled = TryDeposit(idEntity.Value, idComp, args.Used, args.User);
    }

    private bool TryDeposit(EntityUid idCard, IdCardComponent comp, EntityUid cash, EntityUid user)
    {
        if (string.IsNullOrEmpty(comp.AccountNumber))
            return false;

        if (!TryComp<StackComponent>(cash, out var stack) || stack.StackTypeId != CreditStack)
            return false;

        if (!_balance.TryGetByAccount(comp.AccountNumber, out var owner))
        {
            _popup.PopupEntity(Loc.GetString("id-card-account-invalid"), idCard, user, PopupType.MediumCaution);
            return true;
        }

        var amount = stack.Count;
        if (amount <= 0)
            return false;

        _balance.AddBalance(owner, amount, description: Loc.GetString("transaction-deposit"));
        QueueDel(cash);

        _popup.PopupEntity(
            Loc.GetString("id-card-deposit-success", ("amount", amount)),
            idCard,
            user);

        return true;
    }

    private void OnCreateAccount(EntityUid idCard, EntityUid user, ICommonSession session)
    {
        if (!TryComp<IdCardComponent>(idCard, out var comp))
            return;

        if (!string.IsNullOrEmpty(comp.AccountNumber))
        {
            _popup.PopupEntity(Loc.GetString("id-card-account-locked"), idCard, session, PopupType.MediumCaution);
            return;
        }

        var accountNumber = _balance.CreateAccount(user);

        comp.AccountNumber = accountNumber;
        Dirty(idCard, comp);

        _popup.PopupEntity(Loc.GetString("id-card-create-account-success"), idCard, session, PopupType.Medium);
    }

    private void OnAccountEntered(EntityUid idCard, EntityUid user, ICommonSession session, string accountNumber)
    {
        if (!TryComp<IdCardComponent>(idCard, out var comp))
            return;

        if (!string.IsNullOrEmpty(comp.AccountNumber))
        {
            _popup.PopupEntity(Loc.GetString("id-card-account-locked"), idCard, session, PopupType.MediumCaution);
            return;
        }

        accountNumber = accountNumber.Trim().ToUpperInvariant();

        if (string.IsNullOrEmpty(accountNumber))
            return;

        if (!_balance.TryGetByAccount(accountNumber, out _))
        {
            _popup.PopupEntity(Loc.GetString("id-card-set-account-invalid"), idCard, session, PopupType.MediumCaution);
            return;
        }

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
        {
            _popup.PopupEntity(Loc.GetString("id-card-account-invalid"), idCard, session, PopupType.MediumCaution);
            return;
        }

        if (!_balance.TryDeduct(owner, amount, description: Loc.GetString("transaction-withdraw")))
        {
            _popup.PopupEntity(Loc.GetString("id-card-withdraw-insufficient"), idCard, session, PopupType.MediumCaution);
            return;
        }

        var spawned = _stack.SpawnNextToOrDrop(amount, CreditStack, user);
        _hands.TryPickupAnyHand(user, spawned, checkActionBlocker: false);

        _popup.PopupEntity(
            Loc.GetString("id-card-withdraw-success", ("amount", amount)),
            idCard,
            session);
    }
}
