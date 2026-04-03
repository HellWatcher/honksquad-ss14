using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Economy.Components;

/// <summary>
/// Attached to vending machines to sync per-item prices to the client for UI display.
/// Populated server-side by VendingPaymentSystem using estimated cargo values.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class VendingPricesComponent : Component
{
    /// <summary>
    /// Maps entity prototype ID to price in spesos.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<string, int> Prices = new();
}
