using Content.Server.Administration;
using Content.Server.Popups;
using Content.Shared.Access.Components;
using Content.Shared.Database;
using Content.Shared.Popups;
using Content.Shared.RussStation.Economy.Components;
using Content.Shared.Verbs;
using Robust.Shared.Player;

namespace Content.Server.RussStation.Economy;

/// <summary>
/// Allows players to set a bank account number on an ID card via a verb.
/// </summary>
public sealed class IdCardAccountSystem : EntitySystem
{
    [Dependency] private readonly QuickDialogSystem _quickDialog = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly PlayerBalanceSystem _balance = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IdCardComponent, GetVerbsEvent<ActivationVerb>>(OnGetVerbs);
    }

    private void OnGetVerbs(EntityUid uid, IdCardComponent comp, GetVerbsEvent<ActivationVerb> args)
    {
        if (!TryComp(args.User, out ActorComponent? actor))
            return;

        if (!args.CanInteract || !args.CanAccess)
            return;

        var verb = new ActivationVerb
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
        };
        args.Verbs.Add(verb);
    }

    private void OnAccountEntered(EntityUid idCard, EntityUid user, ICommonSession session, string accountNumber)
    {
        if (!HasComp<IdCardComponent>(idCard))
            return;

        accountNumber = accountNumber.Trim();

        if (string.IsNullOrEmpty(accountNumber))
            return;

        // Verify the account number exists.
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
}
