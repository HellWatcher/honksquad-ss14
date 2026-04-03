using Content.Server.Cargo.Systems;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Materials;
using Content.Shared.Popups;
using Content.Shared.Research.Prototypes;
using Content.Shared.RussStation.Economy;
using Content.Shared.RussStation.Economy.Components;
using Content.Shared.Stacks;
using Content.Shared.VendingMachines;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Server.RussStation.Economy;

/// <summary>
/// Handles payment for vending machine purchases using a three-tier pricing system:
/// 1. Recipe material cost × material markup (primary)
/// 2. Cargo estimated value × cargo markup (secondary)
/// 3. Per-vendor minimum derived from cheapest non-zero item (floor)
/// All prices rounded up to nearest 5. Global CVar minimum as last resort.
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

    private float _vendCargoMarkup;
    private float _vendMaterialMarkup;
    private int _vendMinPrice;

    /// <summary>
    /// Cache of entity prototype ID to recipe material cost.
    /// Built once on initialize from all lathe recipes.
    /// </summary>
    private readonly Dictionary<string, double> _recipeMaterialCosts = new();

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, EconomyCCVars.VendCargoMarkup, v => _vendCargoMarkup = v, true);
        Subs.CVar(_cfg, EconomyCCVars.VendMaterialMarkup, v => _vendMaterialMarkup = v, true);
        Subs.CVar(_cfg, EconomyCCVars.VendMinPrice, v => _vendMinPrice = v, true);

        SubscribeLocalEvent<VendingMachineComponent, BeforeVendEvent>(OnBeforeVend);
        SubscribeLocalEvent<VendingMachineComponent, BoundUIOpenedEvent>(OnUIOpened);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);

        BuildRecipeCache();
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<LatheRecipePrototype>() || args.WasModified<MaterialPrototype>())
            BuildRecipeCache();
    }

    /// <summary>
    /// Build a lookup from entity prototype ID to total material cost for its recipe.
    /// </summary>
    private void BuildRecipeCache()
    {
        _recipeMaterialCosts.Clear();

        foreach (var recipe in _proto.EnumeratePrototypes<LatheRecipePrototype>())
        {
            if (recipe.Result == null)
                continue;

            var cost = 0.0;
            foreach (var (materialId, count) in recipe.Materials)
            {
                var material = _proto.Index(materialId);
                cost += material.Price * count;
            }

            // If multiple recipes produce the same item, use the cheapest.
            var resultId = recipe.Result.Value.Id;
            if (!_recipeMaterialCosts.TryGetValue(resultId, out var existing) || cost < existing)
                _recipeMaterialCosts[resultId] = cost;
        }
    }

    private void OnUIOpened(EntityUid uid, VendingMachineComponent comp, BoundUIOpenedEvent args)
    {
        UpdatePrices(uid, comp);
    }

    /// <summary>
    /// Calculate and cache prices for all items in a vending machine.
    /// Two passes: first calculate raw prices, then apply per-vendor minimum.
    /// </summary>
    public void UpdatePrices(EntityUid uid, VendingMachineComponent? comp = null)
    {
        if (!Resolve(uid, ref comp, false))
            return;

        var prices = EnsureComp<VendingPricesComponent>(uid);
        prices.Prices.Clear();

        // Pass 1: calculate raw prices for all items.
        foreach (var itemId in comp.Inventory.Keys)
            prices.Prices[itemId] = GetRawItemPrice(itemId);

        foreach (var itemId in comp.EmaggedInventory.Keys)
            prices.Prices.TryAdd(itemId, GetRawItemPrice(itemId));

        foreach (var itemId in comp.ContrabandInventory.Keys)
            prices.Prices.TryAdd(itemId, GetRawItemPrice(itemId));

        // Pass 2: find cheapest non-zero price as vendor minimum.
        var vendorMin = int.MaxValue;
        foreach (var price in prices.Prices.Values)
        {
            if (price > 0 && price < vendorMin)
                vendorMin = price;
        }

        // Fall back to global CVar if no items had a price.
        if (vendorMin == int.MaxValue)
            vendorMin = RoundUpTo5(_vendMinPrice);

        // Pass 3: apply vendor minimum and round.
        foreach (var itemId in prices.Prices.Keys)
        {
            var price = prices.Prices[itemId];
            prices.Prices[itemId] = Math.Max(price, vendorMin);
        }

        Dirty(uid, prices);
    }

    private void OnBeforeVend(EntityUid uid, VendingMachineComponent comp, ref BeforeVendEvent args)
    {
        // Look up cached price.
        if (!TryComp<VendingPricesComponent>(uid, out var prices)
            || !prices.Prices.TryGetValue(args.ItemId, out var price)
            || price <= 0)
        {
            return;
        }

        // Try ID-based account payment first.
        if (TryPayByAccount(args.User, price))
            return;

        // Try paying with physical spesos in hand.
        if (TryPayByCash(args.User, price))
            return;

        // Can't pay.
        var currentBalance = GetAvailableFunds(args.User);
        _popup.PopupEntity(
            Loc.GetString("vending-machine-insufficient-funds", ("cost", price), ("balance", currentBalance)),
            uid,
            args.User);
        args.Cancelled = true;
    }

    private int GetAvailableFunds(EntityUid buyer)
    {
        var funds = 0;

        // Account balance.
        if (_idCard.TryFindIdCard(buyer, out var idCard)
            && TryComp<IdCardComponent>(idCard, out var id)
            && !string.IsNullOrEmpty(id.AccountNumber)
            && _balance.TryGetByAccount(id.AccountNumber, out var owner))
        {
            funds += _balance.GetBalance(owner);
        }
        else if (TryComp<PlayerBalanceComponent>(buyer, out var directBalance))
        {
            funds += directBalance.Balance;
        }

        // Cash in hand.
        foreach (var held in _hands.EnumerateHeld(buyer))
        {
            if (TryComp<StackComponent>(held, out var stack) && stack.StackTypeId == CreditStack)
                funds += stack.Count;
        }

        return funds;
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

        return remaining <= 0;
    }

    /// <summary>
    /// Calculate the raw vending price for an item (before per-vendor minimum).
    /// Uses max of: recipe material cost × material markup, cargo value × cargo markup.
    /// Result is rounded up to nearest 5.
    /// </summary>
    private int GetRawItemPrice(string itemId)
    {
        var materialPrice = 0.0;
        var cargoPrice = 0.0;

        // Primary: recipe material cost.
        if (_recipeMaterialCosts.TryGetValue(itemId, out var materialCost))
            materialPrice = materialCost * _vendMaterialMarkup;

        // Secondary: cargo estimated value.
        if (_proto.TryIndex<EntityPrototype>(itemId, out var proto))
            cargoPrice = _pricing.GetEstimatedPrice(proto) * _vendCargoMarkup;

        var rawPrice = Math.Max(materialPrice, cargoPrice);

        return RoundUpTo5((int) Math.Ceiling(rawPrice));
    }

    private static int RoundUpTo5(int value)
    {
        if (value <= 0)
            return 0;

        var remainder = value % 5;
        return remainder == 0 ? value : value + (5 - remainder);
    }
}
