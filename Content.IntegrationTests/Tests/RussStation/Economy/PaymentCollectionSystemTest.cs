using Content.Server.RussStation.Economy;
using Content.Shared.RussStation.Economy.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.RussStation.Economy;

/// <summary>
/// Covers the consolidated payment seam (#866). These exercise the
/// ID-card -> account resolution and account-debit logic that vending and
/// ID-card interactions previously each re-implemented against
/// <see cref="PlayerBalanceSystem"/> directly. The existing vending tests only
/// hit the direct-on-mob balance fallback, so the card-linked path lives here.
/// </summary>
[TestFixture]
[TestOf(typeof(PaymentCollectionSystem))]
public sealed class PaymentCollectionSystemTest
{
    /// <summary>
    /// A card linked to a live account resolves to the owning entity and its balance.
    /// </summary>
    [Test]
    public async Task CardResolvesToLinkedAccountTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var balance = entMan.System<PlayerBalanceSystem>();
            var payment = entMan.System<PaymentCollectionSystem>();

            // Owner mob with a real account registered in the ledger index.
            var owner = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var account = balance.CreateAccount(owner);
            balance.SetBalance(owner, 500);

            // A card stamped with that account number.
            var card = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            entMan.AddComponent<BankLinkedCardComponent>(card).AccountNumber = account;

            Assert.That(payment.TryGetAccountByCard(card, out var resolved, out var comp), Is.True,
                "A card linked to a live account should resolve.");
            Assert.That(resolved, Is.EqualTo(owner), "Resolution should point at the account owner.");
            Assert.That(comp.Balance, Is.EqualTo(500), "Resolved balance component should be the owner's.");

            entMan.DeleteEntity(card);
            entMan.DeleteEntity(owner);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Cards with no link, or a stale/unknown account number, do not resolve.
    /// </summary>
    [Test]
    public async Task UnlinkedOrStaleCardDoesNotResolveTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var payment = entMan.System<PaymentCollectionSystem>();

            // No BankLinkedCardComponent at all.
            var blank = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            Assert.That(payment.TryGetAccountByCard(blank, out _, out _), Is.False,
                "A card with no linked account should not resolve.");

            // Linked, but to an account number that was never registered.
            var stale = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            entMan.AddComponent<BankLinkedCardComponent>(stale).AccountNumber = "DEADBEEF";
            Assert.That(payment.TryGetAccountByCard(stale, out _, out _), Is.False,
                "A card pointing at an unknown account should not resolve.");

            entMan.DeleteEntity(blank);
            entMan.DeleteEntity(stale);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// A mob carrying a balance directly (no ID) is the resolution fallback.
    /// </summary>
    [Test]
    public async Task DirectMobBalanceIsFallbackTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var balance = entMan.System<PlayerBalanceSystem>();
            var payment = entMan.System<PaymentCollectionSystem>();

            var mob = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            entMan.AddComponent<PlayerBalanceComponent>(mob);
            balance.SetBalance(mob, 250);

            Assert.That(payment.TryGetAccount(mob, out var owner, out var comp), Is.True,
                "A mob with a direct balance should resolve to itself.");
            Assert.That(owner, Is.EqualTo(mob));
            Assert.That(comp.Balance, Is.EqualTo(250));
            Assert.That(payment.GetAvailableFunds(mob), Is.EqualTo(250),
                "Available funds with no cash in hand should equal the account balance.");

            entMan.DeleteEntity(mob);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Account payment deducts when affordable and is a no-op when it is not.
    /// </summary>
    [Test]
    public async Task TryPayByAccountDeductsAndRespectsBalanceTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var balance = entMan.System<PlayerBalanceSystem>();
            var payment = entMan.System<PaymentCollectionSystem>();

            var mob = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            entMan.AddComponent<PlayerBalanceComponent>(mob);
            balance.SetBalance(mob, 100);

            Assert.That(payment.TryPayByAccount(mob, 30), Is.True, "An affordable charge should succeed.");
            Assert.That(balance.GetBalance(mob), Is.EqualTo(70), "Balance should drop by the charged amount.");

            Assert.That(payment.TryPayByAccount(mob, 1000), Is.False,
                "An unaffordable charge should fail.");
            Assert.That(balance.GetBalance(mob), Is.EqualTo(70),
                "A failed charge should leave the balance untouched.");

            entMan.DeleteEntity(mob);
        });

        await pair.CleanReturnAsync();
    }
}
