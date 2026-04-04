using Robust.Shared.Serialization;

namespace Content.Shared.RussStation.Economy;

[Serializable, NetSerializable]
public sealed class BalanceCartridgeUiState : BoundUserInterfaceState
{
    public int Balance;
    public string AccountSuffix;
    public bool PaycheckMuted;
    public List<TransactionRecord> Transactions;
    public bool HasId;

    public BalanceCartridgeUiState(int balance, string accountSuffix, bool paycheckMuted, List<TransactionRecord> transactions, bool hasId)
    {
        Balance = balance;
        AccountSuffix = accountSuffix;
        PaycheckMuted = paycheckMuted;
        Transactions = transactions;
        HasId = hasId;
    }
}
