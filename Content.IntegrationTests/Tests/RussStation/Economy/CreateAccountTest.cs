using Content.IntegrationTests.Tests.Interaction;
using Content.Server.RussStation.Economy;
using Content.Shared.Access.Components;
using Content.Shared.RussStation.Economy.Components;

namespace Content.IntegrationTests.Tests.RussStation.Economy;

public sealed class CreateAccountTest : InteractionTest
{
    /// <summary>
    /// Creating an account on a mob without one should generate a new account
    /// and stamp it on the ID card.
    /// </summary>
    [Test]
    public async Task CreateAccountOnBlankIdTest()
    {
        // Spawn an ID card as the target.
        await SpawnTarget("PassengerIDCard");
        var idEnt = SEntMan.GetEntity(Target!.Value);

        await Server.WaitPost(() =>
        {
            var balanceSys = SEntMan.System<PlayerBalanceSystem>();

            // Player should not have an account yet.
            Assert.That(SEntMan.HasComponent<PlayerBalanceComponent>(SPlayer), Is.False);

            // ID should be blank.
            var idComp = SEntMan.GetComponent<IdCardComponent>(idEnt);
            Assert.That(string.IsNullOrEmpty(idComp.AccountNumber), Is.True);

            // Create account.
            var accountNumber = balanceSys.CreateAccount(SPlayer);
            Assert.That(accountNumber, Is.Not.Null.And.Not.Empty);

            // Player should now have a balance component with the account.
            var balanceComp = SEntMan.GetComponent<PlayerBalanceComponent>(SPlayer);
            Assert.That(balanceComp.AccountNumber, Is.EqualTo(accountNumber));
            Assert.That(balanceComp.Balance, Is.EqualTo(0), "New account should start with zero balance.");
        });
    }

    /// <summary>
    /// Creating a new account should invalidate the old one.
    /// The old account number should no longer resolve.
    /// </summary>
    [Test]
    public async Task CreateAccountInvalidatesOldTest()
    {
        await SpawnTarget("PassengerIDCard");

        await Server.WaitPost(() =>
        {
            var balanceSys = SEntMan.System<PlayerBalanceSystem>();

            // Create the first account.
            var oldAccount = balanceSys.CreateAccount(SPlayer);
            Assert.That(oldAccount, Is.Not.Null);

            // Old account should resolve.
            Assert.That(balanceSys.TryGetByAccount(oldAccount, out _), Is.True);

            // Create a second account (simulating stolen ID replacement).
            var newAccount = balanceSys.CreateAccount(SPlayer);
            Assert.That(newAccount, Is.Not.Null);
            Assert.That(newAccount, Is.Not.EqualTo(oldAccount));

            // Old account should no longer resolve.
            Assert.That(balanceSys.TryGetByAccount(oldAccount, out _), Is.False,
                "Old account should be invalidated after creating a new one.");

            // New account should resolve.
            Assert.That(balanceSys.TryGetByAccount(newAccount, out _), Is.True);
        });
    }

    /// <summary>
    /// Creating a new account should wipe the old balance to zero.
    /// </summary>
    [Test]
    public async Task CreateAccountWipesBalanceTest()
    {
        await SpawnTarget("PassengerIDCard");

        await Server.WaitPost(() =>
        {
            var balanceSys = SEntMan.System<PlayerBalanceSystem>();

            // Create account and add extra funds.
            balanceSys.CreateAccount(SPlayer);
            balanceSys.AddBalance(SPlayer, 5000);
            var balanceComp = SEntMan.GetComponent<PlayerBalanceComponent>(SPlayer);
            Assert.That(balanceComp.Balance, Is.EqualTo(5000));

            // Create new account, balance should reset to zero.
            balanceSys.CreateAccount(SPlayer);
            Assert.That(balanceComp.Balance, Is.EqualTo(0),
                "New account should wipe balance to zero.");
        });
    }

    /// <summary>
    /// An ID card with an invalidated account number should fail lookups.
    /// </summary>
    [Test]
    public async Task InvalidatedIdFailsLookupTest()
    {
        await SpawnTarget("PassengerIDCard");
        var idEnt = SEntMan.GetEntity(Target!.Value);

        await Server.WaitPost(() =>
        {
            var balanceSys = SEntMan.System<PlayerBalanceSystem>();

            // Create account and stamp it on the ID.
            var oldAccount = balanceSys.CreateAccount(SPlayer);
            var idComp = SEntMan.GetComponent<IdCardComponent>(idEnt);
            idComp.AccountNumber = oldAccount;

            // Verify it resolves.
            Assert.That(balanceSys.TryGetByAccount(idComp.AccountNumber, out _), Is.True);

            // Create a new account (invalidates old).
            balanceSys.CreateAccount(SPlayer);

            // The ID still has the old account number, which should no longer resolve.
            Assert.That(balanceSys.TryGetByAccount(idComp.AccountNumber, out _), Is.False,
                "ID with old account number should fail lookup after invalidation.");
        });
    }

    /// <summary>
    /// An ID card that already has an account number set should not be overwritten
    /// by creating a new account. The old account number persists on the ID.
    /// </summary>
    [Test]
    public async Task LockedIdCannotBeOverwrittenTest()
    {
        await SpawnTarget("PassengerIDCard");
        var idEnt = SEntMan.GetEntity(Target!.Value);

        await Server.WaitPost(() =>
        {
            var balanceSys = SEntMan.System<PlayerBalanceSystem>();
            var idComp = SEntMan.GetComponent<IdCardComponent>(idEnt);

            // Create an account and stamp it on the ID (simulates normal spawn).
            var firstAccount = balanceSys.CreateAccount(SPlayer);
            idComp.AccountNumber = firstAccount;

            // Create a second account (simulates "Create Account" verb).
            // The mob gets a new account, but the ID should still have the old number.
            var secondAccount = balanceSys.CreateAccount(SPlayer);
            Assert.That(secondAccount, Is.Not.EqualTo(firstAccount));

            // ID still has the first account number (locked).
            Assert.That(idComp.AccountNumber, Is.EqualTo(firstAccount),
                "ID with account set should not be overwritten by creating a new account.");

            // The old account on the ID is now invalid (invalidated by second create).
            Assert.That(balanceSys.TryGetByAccount(firstAccount, out _), Is.False,
                "Old account should be invalidated after creating a new one.");
        });
    }
}
