using Robust.Shared.GameObjects;

namespace Content.Shared.RussStation.Economy.Components;

/// <summary>
/// Added at runtime to ID cards that have been linked to a bank account.
/// Holds the account number separately from the upstream IdCardComponent to avoid modifying it.
/// </summary>
[RegisterComponent]
public sealed partial class BankLinkedCardComponent : Component
{
    [DataField]
    public string? AccountNumber;
}
