using Content.Client.UserInterface.Fragments;
using Content.Shared.CartridgeLoader;
using Content.Shared.RussStation.Messenger;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;

namespace Content.Client.RussStation.Messenger;

public sealed partial class MessengerCartridgeUi : UIFragment
{
    private MessengerCartridgeUiFragment? _fragment;

    public override Control GetUIFragmentRoot() => _fragment!;

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        if (_fragment is { Disposed: false })
            return;

        _fragment = new MessengerCartridgeUiFragment();

        _fragment.OnContactSelected += target =>
        {
            var message = new CartridgeUiMessage(new MessengerOpenConversationMessage(target));
            userInterface.SendMessage(message);
        };

        _fragment.OnSendMessage += (target, text) =>
        {
            var message = new CartridgeUiMessage(new MessengerSendMessage(target, text));
            userInterface.SendMessage(message);
        };

        _fragment.OnBackPressed += () =>
        {
            var message = new CartridgeUiMessage(new MessengerRequestContactsMessage());
            userInterface.SendMessage(message);
        };

        _fragment.OnMuteToggled += () =>
        {
            var message = new CartridgeUiMessage(new MessengerToggleMuteMessage());
            userInterface.SendMessage(message);
        };
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not MessengerUiState messengerState)
            return;

        _fragment?.UpdateState(messengerState);
    }
}
