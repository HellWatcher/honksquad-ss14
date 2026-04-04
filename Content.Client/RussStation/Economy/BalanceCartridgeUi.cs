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
        if (_fragment is { Disposed: false })
            return;

        _fragment = new BalanceCartridgeUiFragment();
        _fragment.OnMuteToggled += () =>
        {
            var message = new CartridgeUiMessage(new TogglePaycheckMuteMessage());
            userInterface.SendMessage(message);
        };
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not BalanceCartridgeUiState balanceState)
            return;

        _fragment?.UpdateState(balanceState.Balance, balanceState.AccountSuffix, balanceState.PaycheckMuted, balanceState.Transactions, balanceState.HasId);
    }
}
