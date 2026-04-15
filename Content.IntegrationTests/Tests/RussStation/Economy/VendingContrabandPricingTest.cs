using Content.IntegrationTests.Fixtures;
using Content.Server.RussStation.Economy;
using Content.Shared.RussStation.Economy.Components;
using Content.Shared.VendingMachines;

namespace Content.IntegrationTests.Tests.RussStation.Economy;

/// <summary>
/// Verifies that emag and contraband vending items are not priced.
/// </summary>
[TestOf(typeof(VendingPaymentSystem))]
public sealed class VendingContrabandPricingTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  parent: BaseItem
  id: ContrabandTestRegularItem
  name: ContrabandTestRegularItem

- type: entity
  parent: BaseItem
  id: ContrabandTestEmagItem
  name: ContrabandTestEmagItem

- type: entity
  parent: BaseItem
  id: ContrabandTestContrabandItem
  name: ContrabandTestContrabandItem

- type: vendingMachineInventory
  id: ContrabandTestInventory
  startingInventory:
    ContrabandTestRegularItem: 5
  emaggedInventory:
    ContrabandTestEmagItem: 3
  contrabandInventory:
    ContrabandTestContrabandItem: 3

- type: entity
  id: ContrabandTestVendor
  parent: VendingMachine
  components:
  - type: VendingMachine
    pack: ContrabandTestInventory
    ejectDelay: 0
  - type: Sprite
    sprite: error.rsi
";

    [Test]
    public async Task EmagAndContrabandItemsHaveNoPriceTest()
    {
        var server = Server;
        var entMan = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var vendor = entMan.Spawn("ContrabandTestVendor");
            var paymentSystem = entMan.System<VendingPaymentSystem>();

            paymentSystem.UpdatePrices(vendor);

            var prices = entMan.GetComponent<VendingPricesComponent>(vendor);

            // Regular items should have a price.
            Assert.That(prices.Prices.ContainsKey("ContrabandTestRegularItem"), Is.True,
                "Regular item should be priced.");

            // Emag items should NOT have a price.
            Assert.That(prices.Prices.ContainsKey("ContrabandTestEmagItem"), Is.False,
                "Emag item should not be priced.");

            // Contraband items should NOT have a price.
            Assert.That(prices.Prices.ContainsKey("ContrabandTestContrabandItem"), Is.False,
                "Contraband item should not be priced.");

            entMan.DeleteEntity(vendor);
        });
    }
}
