using Robust.Shared.Serialization;

namespace Content.Shared.RussStation.Economy;

[Serializable, NetSerializable]
public sealed class TransactionRecord
{
    public int Amount;
    public string Description;
    public TimeSpan Timestamp;

    public TransactionRecord(int amount, string description, TimeSpan timestamp)
    {
        Amount = amount;
        Description = description;
        Timestamp = timestamp;
    }
}
