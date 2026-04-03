using Content.Shared.Popups;
using Content.Shared.RussStation.Economy;
using Content.Shared.RussStation.Economy.Components;
using Content.Shared.VendingMachines;
using Robust.Shared.Configuration;

namespace Content.Server.RussStation.Economy;

/// <summary>
/// Handles payment for vending machine purchases using the player balance system.
/// Subscribes to <see cref="BeforeVendEvent"/> raised by the vending machine system.
/// </summary>
public sealed class VendingPaymentSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly PlayerBalanceSystem _balance = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private int _vendPrice;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, EconomyCCVars.VendPrice, v => _vendPrice = v, true);

        SubscribeLocalEvent<VendingMachineComponent, BeforeVendEvent>(OnBeforeVend);
    }

    private void OnBeforeVend(EntityUid uid, VendingMachineComponent comp, ref BeforeVendEvent args)
    {
        if (_vendPrice <= 0)
            return;

        if (!TryComp<PlayerBalanceComponent>(args.User, out var balance))
        {
            args.Cancelled = true;
            return;
        }

        if (!_balance.TryDeduct(args.User, _vendPrice, balance))
        {
            _popup.PopupEntity(
                Loc.GetString("vending-machine-insufficient-funds", ("cost", _vendPrice), ("balance", balance.Balance)),
                uid,
                args.User);
            args.Cancelled = true;
        }
    }
}
