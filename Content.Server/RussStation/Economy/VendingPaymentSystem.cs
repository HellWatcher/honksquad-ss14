using Content.Server.Cargo.Systems;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Popups;
using Content.Shared.RussStation.Economy;
using Content.Shared.RussStation.Economy.Components;
using Content.Shared.VendingMachines;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Server.RussStation.Economy;

/// <summary>
/// Handles payment for vending machine purchases using estimated item pricing.
/// Reads the buyer's account from their ID card and resolves it to a balance.
/// </summary>
public sealed class VendingPaymentSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly PricingSystem _pricing = default!;
    [Dependency] private readonly PlayerBalanceSystem _balance = default!;
    [Dependency] private readonly SharedIdCardSystem _idCard = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private float _vendMarkup;
    private int _vendMinPrice;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, EconomyCCVars.VendMarkup, v => _vendMarkup = v, true);
        Subs.CVar(_cfg, EconomyCCVars.VendMinPrice, v => _vendMinPrice = v, true);

        SubscribeLocalEvent<VendingMachineComponent, BeforeVendEvent>(OnBeforeVend);
        SubscribeLocalEvent<VendingMachineComponent, BoundUIOpenedEvent>(OnUIOpened);
    }

    private void OnUIOpened(EntityUid uid, VendingMachineComponent comp, BoundUIOpenedEvent args)
    {
        UpdatePrices(uid, comp);
    }

    /// <summary>
    /// Calculate and cache prices for all items in a vending machine.
    /// </summary>
    public void UpdatePrices(EntityUid uid, VendingMachineComponent? comp = null)
    {
        if (!Resolve(uid, ref comp, false))
            return;

        var prices = EnsureComp<VendingPricesComponent>(uid);
        prices.Prices.Clear();

        foreach (var itemId in comp.Inventory.Keys)
            prices.Prices[itemId] = GetItemPrice(itemId);

        foreach (var itemId in comp.EmaggedInventory.Keys)
            prices.Prices.TryAdd(itemId, GetItemPrice(itemId));

        foreach (var itemId in comp.ContrabandInventory.Keys)
            prices.Prices.TryAdd(itemId, GetItemPrice(itemId));

        Dirty(uid, prices);
    }

    private void OnBeforeVend(EntityUid uid, VendingMachineComponent comp, ref BeforeVendEvent args)
    {
        if (_vendMarkup <= 0)
            return;

        var price = GetItemPrice(args.ItemId);
        if (price <= 0)
            return;

        // Resolve buyer's balance via their ID card's account number.
        // If the buyer has no balance at all (not a player), let them vend for free.
        if (!TryGetBuyerBalance(args.User, out var owner, out var balanceComp))
            return;

        if (!_balance.TryDeduct(owner, price, balanceComp))
        {
            _popup.PopupEntity(
                Loc.GetString("vending-machine-insufficient-funds", ("cost", price), ("balance", balanceComp!.Balance)),
                uid,
                args.User);
            args.Cancelled = true;
        }
    }

    /// <summary>
    /// Calculate the vending price for an item based on its estimated cargo value.
    /// </summary>
    public int GetItemPrice(string itemId)
    {
        if (!_proto.TryIndex<EntityPrototype>(itemId, out var proto))
            return _vendMinPrice;

        var estimated = _pricing.GetEstimatedPrice(proto);
        var price = (int) Math.Ceiling(estimated * _vendMarkup);

        return Math.Max(price, _vendMinPrice);
    }

    /// <summary>
    /// Find the buyer's balance by reading the account number off their ID card.
    /// Falls back to direct mob lookup if no ID card is found.
    /// </summary>
    private bool TryGetBuyerBalance(EntityUid buyer, out EntityUid owner, out PlayerBalanceComponent? balance)
    {
        // Try ID-based lookup first.
        if (_idCard.TryFindIdCard(buyer, out var idCard)
            && TryComp<IdCardComponent>(idCard, out var id)
            && !string.IsNullOrEmpty(id.AccountNumber)
            && _balance.TryGetByAccount(id.AccountNumber, out owner))
        {
            balance = CompOrNull<PlayerBalanceComponent>(owner);
            return balance != null;
        }

        // Fallback: direct mob lookup (for cases without an ID).
        owner = buyer;
        return TryComp(buyer, out balance);
    }
}
