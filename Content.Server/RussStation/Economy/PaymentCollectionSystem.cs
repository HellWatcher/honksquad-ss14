using System.Diagnostics.CodeAnalysis;
using Content.Shared.Access.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.RussStation.Economy.Components;
using Content.Shared.Stacks;
using Robust.Shared.Prototypes;
using SharedEconomyConstants = Content.Shared.RussStation.Economy.EconomyConstants;

namespace Content.Server.RussStation.Economy;

/// <summary>
/// Single seam for collecting payment from a buyer. Owns the account-resolution,
/// account-debit, and physical-cash logic that vending and ID-card interactions
/// previously each re-implemented against <see cref="PlayerBalanceSystem"/> directly.
///
/// Consumers (e.g. <see cref="VendingPaymentSystem"/>, <see cref="IdCardAccountSystem"/>)
/// go through this system instead of reaching into the ledger's account index,
/// keeping the "how do I find and charge an account" knowledge in one place.
/// </summary>
public sealed partial class PaymentCollectionSystem : EntitySystem
{
    [Dependency] private PlayerBalanceSystem _balance = default!;
    [Dependency] private SharedIdCardSystem _idCard = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedStackSystem _stacks = default!;

    /// <summary>
    /// Resolve the balance account linked to an ID card. Returns false when the
    /// card has no account number or the number does not map to a live account.
    /// </summary>
    public bool TryGetAccountByCard(EntityUid idCard, out EntityUid owner, [NotNullWhen(true)] out PlayerBalanceComponent? comp)
    {
        owner = default;
        comp = null;

        if (TryComp<BankLinkedCardComponent>(idCard, out var bankCard)
            && !string.IsNullOrEmpty(bankCard.AccountNumber)
            && _balance.TryGetByAccount(bankCard.AccountNumber, out owner))
        {
            return TryComp(owner, out comp);
        }

        return false;
    }

    /// <summary>
    /// Resolve the account a holder can spend from. Prefers the account linked to
    /// the holder's ID card, falling back to a balance carried directly on the mob
    /// (e.g. an ID-less NPC).
    /// </summary>
    public bool TryGetAccount(EntityUid holder, out EntityUid owner, [NotNullWhen(true)] out PlayerBalanceComponent? comp)
    {
        if (_idCard.TryFindIdCard(holder, out var idCard) && TryGetAccountByCard(idCard, out owner, out comp))
            return true;

        if (TryComp(holder, out comp))
        {
            owner = holder;
            return true;
        }

        owner = default;
        comp = null;
        return false;
    }

    /// <summary>
    /// Try to charge a price to the buyer's bank account. Returns false if no
    /// account is resolvable or it has insufficient funds.
    /// </summary>
    public bool TryPayByAccount(EntityUid buyer, int price, string? description = null)
    {
        if (TryGetAccount(buyer, out var owner, out var comp))
            return _balance.TryDeduct(owner, price, comp, description);

        return false;
    }

    /// <summary>
    /// Try to pay a price using physical spesos held in the buyer's hands.
    /// Consumes stacks until the price is met. Returns false if not enough cash
    /// is held (and leaves the partially-spent stacks as-is).
    /// </summary>
    public bool TryPayByCash(EntityUid buyer, int price)
    {
        var remaining = price;

        foreach (var held in _hands.EnumerateHeld(buyer))
        {
            if (!TryComp<StackComponent>(held, out var stack) || stack.StackTypeId != SharedEconomyConstants.CreditStack)
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
    /// Total spesos the buyer could spend right now: their account balance plus
    /// any physical credits held in hand.
    /// </summary>
    public int GetAvailableFunds(EntityUid buyer)
    {
        var funds = 0;

        // Account balance.
        if (TryGetAccount(buyer, out var owner, out _))
            funds += _balance.GetBalance(owner);

        // Cash in hand.
        foreach (var held in _hands.EnumerateHeld(buyer))
        {
            if (TryComp<StackComponent>(held, out var stack) && stack.StackTypeId == SharedEconomyConstants.CreditStack)
                funds += stack.Count;
        }

        return funds;
    }
}
