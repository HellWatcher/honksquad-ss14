using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server.Administration;
using Content.Server.RussStation.Economy;
using Content.Server.Verbs;
using Content.Shared.Administration;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.RussStation.Economy.Components;
using Content.Shared.Stacks;
using Content.Shared.Verbs;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.IntegrationTests.Tests.RussStation.Economy;

/// <summary>
/// End-to-end coverage of <see cref="IdCardAccountSystem"/> cash flows, which #866
/// re-pointed at <see cref="PaymentCollectionSystem"/> for card -> account
/// resolution. Deposit runs through the real interaction (cash used on the card);
/// withdraw runs through the real verb and quick-dialog round trip.
/// </summary>
public sealed class IdCardAccountInteractionTest : InteractionTest
{
    private const string IdCardProtoId = "PassengerIDCard";

    /// <summary>
    /// Using spesos on an ID card linked to a live account credits that account
    /// and consumes the cash.
    /// </summary>
    [Test]
    public async Task DepositCreditsLinkedAccountTest()
    {
        await SpawnTarget(IdCardProtoId);
        var idEnt = ToServer(Target!.Value);

        await Server.WaitPost(() =>
        {
            var balance = SEntMan.System<PlayerBalanceSystem>();
            // CreateAccount resets balance to zero; link the spawned card to it.
            var account = balance.CreateAccount(SPlayer);
            SEntMan.EnsureComponent<BankLinkedCardComponent>(idEnt).AccountNumber = account;
        });

        // Use a 200-speso stack on the card.
        await InteractUsing("SpaceCash", 200);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.System<PlayerBalanceSystem>().GetBalance(SPlayer), Is.EqualTo(200),
                "Deposit should credit the card's linked account.");
            Assert.That(PlayerHoldsCredits(out _), Is.False,
                "Deposited cash should be consumed.");
        });
    }

    /// <summary>
    /// Depositing onto a card whose account number no longer resolves credits
    /// nobody and leaves the cash untouched.
    /// </summary>
    [Test]
    public async Task DepositToStaleAccountIsNoOpTest()
    {
        await SpawnTarget(IdCardProtoId);
        var idEnt = ToServer(Target!.Value);

        await Server.WaitPost(() =>
        {
            var balance = SEntMan.System<PlayerBalanceSystem>();
            // The player has a real account, but the card points at an unknown one.
            balance.CreateAccount(SPlayer);
            balance.SetBalance(SPlayer, 0);
            SEntMan.EnsureComponent<BankLinkedCardComponent>(idEnt).AccountNumber = "DEADBEEF";
        });

        await InteractUsing("SpaceCash", 200);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.System<PlayerBalanceSystem>().GetBalance(SPlayer), Is.EqualTo(0),
                "An unresolvable card account should not be credited.");
            Assert.That(PlayerHoldsCredits(out var count), Is.True, "Cash should not be consumed on a failed deposit.");
            Assert.That(count, Is.EqualTo(200));
        });
    }

    /// <summary>
    /// The withdraw verb, answered with an amount, deducts from the owner's
    /// account and hands back physical spesos.
    /// </summary>
    [Test]
    public async Task WithdrawDeductsOwnerAccountTest()
    {
        // Hold an ID card linked to the player's own account.
        var idEnt = ToServer(await PlaceInHands(IdCardProtoId));

        await Server.WaitPost(() =>
        {
            var balance = SEntMan.System<PlayerBalanceSystem>();
            var account = balance.CreateAccount(SPlayer);
            balance.SetBalance(SPlayer, 500);
            SEntMan.EnsureComponent<BankLinkedCardComponent>(idEnt).AccountNumber = account;
        });

        // Execute the withdraw alt-verb; it opens an amount dialog for the session.
        var dialogId = 0;
        await Server.WaitPost(() =>
        {
            var verbs = SEntMan.System<VerbSystem>();
            var withdraw = verbs.GetLocalVerbs(idEnt, SPlayer, typeof(AlternativeVerb))
                .FirstOrDefault(v => v.Text == Loc.GetString("id-card-withdraw-verb"));

            Assert.That(withdraw, Is.Not.Null, "A card linked to a live account should offer the withdraw verb.");
            verbs.ExecuteVerb(withdraw!, SPlayer, idEnt);

            dialogId = LatestDialogId(ServerSession);
        });

        // Answer the dialog from the client, as the real UI would.
        await Client.WaitPost(() =>
        {
            Client.ResolveDependency<IEntityNetworkManager>().SendSystemNetworkMessage(
                new QuickDialogResponseEvent(
                    dialogId,
                    new Dictionary<string, string> { ["1"] = "200" },
                    QuickDialogButtonFlag.OkButton));
        });
        await RunTicks(5);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.System<PlayerBalanceSystem>().GetBalance(SPlayer), Is.EqualTo(300),
                "Withdrawing 200 from a 500 balance should leave 300.");
        });
    }

    /// <summary>
    /// Reads the most recently opened quick-dialog id for a session. The server
    /// hands these out internally, so a test playing the client's part has to
    /// peek at them to reply with a valid id.
    /// </summary>
    private int LatestDialogId(ICommonSession session)
    {
        var quickDialog = SEntMan.System<QuickDialogSystem>();
        var field = typeof(QuickDialogSystem).GetField("_openDialogsByUser", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(field, Is.Not.Null, "QuickDialogSystem._openDialogsByUser field not found -- did it get renamed?");

        var byUser = (Dictionary<NetUserId, List<int>>) field!.GetValue(quickDialog)!;
        return byUser[session.UserId].Last();
    }

    /// <summary>
    /// True if the player is holding a credit stack; reports its count.
    /// Must be called on the server thread.
    /// </summary>
    private bool PlayerHoldsCredits(out int count)
    {
        count = 0;
        foreach (var held in SEntMan.System<SharedHandsSystem>().EnumerateHeld(SPlayer))
        {
            if (SEntMan.TryGetComponent<StackComponent>(held, out var stack))
            {
                count = stack.Count;
                return true;
            }
        }

        return false;
    }
}
