using System.Linq;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server.RussStation.Economy;
using Content.Server.VendingMachines;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.RussStation.Economy.Components;
using Content.Shared.Stacks;
using Content.Shared.VendingMachines;

namespace Content.IntegrationTests.Tests.RussStation.Economy;

public sealed class VendingPaymentTest : InteractionTest
{
    private const string VendingMachineProtoId = "PaymentTestVendingMachine";
    private const string VendedItemProtoId = "PaymentTestItem";

    [TestPrototypes]
    private const string TestPrototypes = $@"
- type: entity
  parent: BaseItem
  id: {VendedItemProtoId}
  name: {VendedItemProtoId}

- type: vendingMachineInventory
  id: PaymentTestVendingInventory
  startingInventory:
    {VendedItemProtoId}: 5

- type: entity
  id: {VendingMachineProtoId}
  parent: VendingMachine
  components:
  - type: VendingMachine
    pack: PaymentTestVendingInventory
    ejectDelay: 0
  - type: Sprite
    sprite: error.rsi
";

    /// <summary>
    /// Mobs without balance or cash are not economy participants and vend for free.
    /// </summary>
    [Test]
    public async Task NonParticipantVendsFreeTest()
    {
        await SpawnTarget(VendingMachineProtoId);
        var vendorEnt = SEntMan.GetEntity(Target.Value);

        var vendingSystem = SEntMan.System<VendingMachineSystem>();
        var items = vendingSystem.GetAllInventory(vendorEnt);

        Assert.That(items, Is.Not.Empty);
        Assert.That(items.First().Amount, Is.EqualTo(5));

        // Power and open (no balance, no ID, no cash)
        await SpawnEntity("APCBasic", SEntMan.GetCoordinates(TargetCoords));
        await RunTicks(1);
        await Activate();
        Assert.That(IsUiOpen(VendingMachineUiKey.Key), "BUI failed to open.");

        var ev = new VendingMachineEjectMessage(InventoryType.Regular, VendedItemProtoId);
        await SendBui(VendingMachineUiKey.Key, ev);

        Assert.That(items.First().Amount, Is.EqualTo(4), "Non-participant should vend for free.");
    }

    /// <summary>
    /// Mobs with insufficient balance should be blocked from vending.
    /// </summary>
    [Test]
    public async Task InsufficientFundsBlocksVendTest()
    {
        await SpawnTarget(VendingMachineProtoId);
        var vendorEnt = SEntMan.GetEntity(Target.Value);

        var vendingSystem = SEntMan.System<VendingMachineSystem>();
        var items = vendingSystem.GetAllInventory(vendorEnt);

        Assert.That(items.First().Amount, Is.EqualTo(5));

        // Give the player a balance of 0
        await Server.WaitPost(() =>
        {
            var comp = SEntMan.EnsureComponent<PlayerBalanceComponent>(SPlayer);
            comp.Balance = 0;
        });

        // Power and open
        await SpawnEntity("APCBasic", SEntMan.GetCoordinates(TargetCoords));
        await RunTicks(1);
        await Activate();
        Assert.That(IsUiOpen(VendingMachineUiKey.Key), "BUI failed to open.");

        // Try to dispense
        var ev = new VendingMachineEjectMessage(InventoryType.Regular, VendedItemProtoId);
        await SendBui(VendingMachineUiKey.Key, ev);

        Assert.That(items.First().Amount, Is.EqualTo(5), "Insufficient funds should block vending.");
    }

    /// <summary>
    /// Mobs with sufficient balance should be charged when vending.
    /// </summary>
    [Test]
    public async Task SufficientFundsDeductsTest()
    {
        await SpawnTarget(VendingMachineProtoId);
        var vendorEnt = SEntMan.GetEntity(Target.Value);

        var vendingSystem = SEntMan.System<VendingMachineSystem>();
        var items = vendingSystem.GetAllInventory(vendorEnt);

        Assert.That(items.First().Amount, Is.EqualTo(5));

        // Give the player a large balance
        await Server.WaitPost(() =>
        {
            var comp = SEntMan.EnsureComponent<PlayerBalanceComponent>(SPlayer);
            comp.Balance = 10000;
        });

        // Power and open
        await SpawnEntity("APCBasic", SEntMan.GetCoordinates(TargetCoords));
        await RunTicks(1);
        await Activate();
        Assert.That(IsUiOpen(VendingMachineUiKey.Key), "BUI failed to open.");

        // Dispense
        var ev = new VendingMachineEjectMessage(InventoryType.Regular, VendedItemProtoId);
        await SendBui(VendingMachineUiKey.Key, ev);

        Assert.That(items.First().Amount, Is.EqualTo(4), "Item should be dispensed when balance is sufficient.");

        // Verify balance was deducted
        await Server.WaitPost(() =>
        {
            var comp = SEntMan.GetComponent<PlayerBalanceComponent>(SPlayer);
            Assert.That(comp.Balance, Is.LessThan(10000), "Balance should decrease after purchase.");
        });
    }

    /// <summary>
    /// Mobs holding physical spesos should be able to pay with them.
    /// </summary>
    [Test]
    public async Task CashPaymentTest()
    {
        await SpawnTarget(VendingMachineProtoId);
        var vendorEnt = SEntMan.GetEntity(Target.Value);

        var vendingSystem = SEntMan.System<VendingMachineSystem>();
        var items = vendingSystem.GetAllInventory(vendorEnt);

        Assert.That(items.First().Amount, Is.EqualTo(5));

        // Put spesos in the player's hand
        var spesos = await SpawnEntity("SpaceCash100", SEntMan.GetCoordinates(PlayerCoords));
        await Server.WaitPost(() =>
        {
            var hands = SEntMan.System<SharedHandsSystem>();
            hands.TryPickupAnyHand(SPlayer, spesos, checkActionBlocker: false);
        });
        await RunTicks(1);

        // Power and open
        await SpawnEntity("APCBasic", SEntMan.GetCoordinates(TargetCoords));
        await RunTicks(1);
        await Activate();
        Assert.That(IsUiOpen(VendingMachineUiKey.Key), "BUI failed to open.");

        // Dispense
        var ev = new VendingMachineEjectMessage(InventoryType.Regular, VendedItemProtoId);
        await SendBui(VendingMachineUiKey.Key, ev);

        Assert.That(items.First().Amount, Is.EqualTo(4), "Cash payment should allow vending.");
    }
}
