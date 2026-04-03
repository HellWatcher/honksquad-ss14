using Robust.Shared.Serialization;

namespace Content.Shared.RussStation.Economy;

[Serializable, NetSerializable]
public sealed class BalanceCartridgeUiState : BoundUserInterfaceState
{
    public int Balance;

    public BalanceCartridgeUiState(int balance)
    {
        Balance = balance;
    }
}
