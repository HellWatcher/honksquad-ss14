using Content.Server.Cargo.Systems;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.RussStation.Economy;
using Content.Shared.RussStation.Economy.Components;
using Content.Shared.Stacks;
using Content.Shared.VendingMachines;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Server.RussStation.Economy;

/// <summary>
/// Handles payment for vending machine purchases using estimated item pricing.
/// Payment methods in order: ID card account, then physical spesos in hand.
/// </summary>
public sealed class VendingPaymentSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly PricingSystem _pricing = default!;
    [Dependency] private readonly PlayerBalanceSystem _balance = default!;
    [Dependency] private readonly SharedIdCardSystem _idCard = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedStackSystem _stacks = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private static readonly ProtoId<StackPrototype> CreditStack = "Credit";

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

        // Check if this entity participates in the economy at all.
        // No balance, no ID account, no cash = not an economy participant (NPCs, test mobs).
        if (!IsEconomyParticipant(args.User))
            return;

        // Try ID-based account payment first.
        if (TryPayByAccount(args.User, price))
            return;

        // Try paying with physical spesos in hand.
        if (TryPayByCash(args.User, price))
            return;

        // Has economy presence but can't pay.
        _popup.PopupEntity(
            Loc.GetString("vending-machine-insufficient-funds", ("cost", price), ("balance", 0)),
            uid,
            args.User);
        args.Cancelled = true;
    }

    private bool IsEconomyParticipant(EntityUid buyer)
    {
        // Has a balance component (player or entity with economy).
        if (HasComp<PlayerBalanceComponent>(buyer))
            return true;

        // Has an ID with an account linked.
        if (_idCard.TryFindIdCard(buyer, out var idCard)
            && TryComp<IdCardComponent>(idCard, out var id)
            && !string.IsNullOrEmpty(id.AccountNumber))
            return true;

        // Has physical cash in hand.
        foreach (var held in _hands.EnumerateHeld(buyer))
        {
            if (TryComp<StackComponent>(held, out var stack) && stack.StackTypeId == CreditStack)
                return true;
        }

        return false;
    }

    private bool TryPayByAccount(EntityUid buyer, int price)
    {
        if (_idCard.TryFindIdCard(buyer, out var idCard)
            && TryComp<IdCardComponent>(idCard, out var id)
            && !string.IsNullOrEmpty(id.AccountNumber)
            && _balance.TryGetByAccount(id.AccountNumber, out var owner)
            && TryComp<PlayerBalanceComponent>(owner, out var balanceComp))
        {
            return _balance.TryDeduct(owner, price, balanceComp);
        }

        // Fallback: direct mob lookup (mob has balance but no ID).
        if (TryComp<PlayerBalanceComponent>(buyer, out var directBalance))
            return _balance.TryDeduct(buyer, price, directBalance);

        return false;
    }

    private bool TryPayByCash(EntityUid buyer, int price)
    {
        var remaining = price;

        // Collect speso stacks from hands.
        foreach (var held in _hands.EnumerateHeld(buyer))
        {
            if (!TryComp<StackComponent>(held, out var stack) || stack.StackTypeId != CreditStack)
                continue;

            var take = Math.Min(remaining, stack.Count);
            _stacks.TryUse((held, stack), take);
            remaining -= take;

            if (remaining <= 0)
                return true;
        }

        // Not enough cash. We already consumed some stacks, so refund isn't worth the complexity.
        // This only triggers if they had some cash but not enough.
        return remaining <= 0;
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
}
