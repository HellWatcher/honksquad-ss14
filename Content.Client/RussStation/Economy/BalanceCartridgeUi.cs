using Content.Client.UserInterface.Fragments;
using Content.Shared.CartridgeLoader;
using Content.Shared.RussStation.Economy;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;

namespace Content.Client.RussStation.Economy;

public sealed partial class BalanceCartridgeUi : UIFragment
{
    private BalanceCartridgeUiFragment? _fragment;

    public override Control GetUIFragmentRoot() => _fragment!;

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        _fragment = new BalanceCartridgeUiFragment();
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not BalanceCartridgeUiState balanceState)
            return;

        _fragment?.UpdateState(balanceState.Balance);
    }
}
