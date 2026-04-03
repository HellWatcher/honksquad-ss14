namespace Content.Shared.RussStation.Economy;

/// <summary>
/// Raised on a vending machine before an item is dispensed.
/// Cancel to prevent the vend (e.g. insufficient funds).
/// </summary>
[ByRefEvent]
public record struct BeforeVendEvent(EntityUid Machine, EntityUid User, string ItemId)
{
    public readonly EntityUid Machine = Machine;
    public readonly EntityUid User = User;
    public readonly string ItemId = ItemId;
    public bool Cancelled;
}
