using Robust.Shared.Serialization;

namespace Content.Shared.RussStation.Economy;

[Serializable, NetSerializable]
public sealed class BalanceCartridgeUiState : BoundUserInterfaceState
{
    public int Balance;
    public string AccountSuffix;

    public BalanceCartridgeUiState(int balance, string accountSuffix)
    {
        Balance = balance;
        AccountSuffix = accountSuffix;
    }
}
