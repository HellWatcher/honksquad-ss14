// HONK — Owns the floating chat input widget lifecycle. See issue #577.

using Content.Client.Chat.Managers;
using Content.Client.UserInterface.Systems.Chat;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;

namespace Content.Client.RussStation.Chat;

/// <summary>
/// Spawns and tears down <see cref="FloatingChatInputControl"/> in response to the
/// focus-chat keybind when the floating-input CVar is enabled. Routes submissions to
/// the shared chat manager (same path as the anchored chat box).
/// </summary>
public sealed class FloatingChatInputController : UIController
{
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;

    private FloatingChatInputControl? _active;

    public bool IsActive => _active != null;

    public override void Initialize()
    {
        _player.LocalPlayerAttached += OnLocalPlayerAttached;
    }

    private void OnLocalPlayerAttached(EntityUid newEntity)
    {
        // Keep the widget following whatever entity the session controls now
        // (death -> ghost, admin respawn, etc.). Without this, the anchor stays
        // pinned to the prior entity and the new body can't open a fresh box.
        _active?.Attach(newEntity);
    }

    public void Show(ChatSelectChannel? channel = null)
    {
        if (_active != null)
        {
            // Already showing — just refocus.
            _active.FocusInput();
            return;
        }

        if (_player.LocalEntity is not { } entity)
            return;

        var root = UIManager.ActiveScreen?.FindControl<LayoutContainer>("ViewportContainer");
        if (root == null)
            return;

        _active = new FloatingChatInputControl();
        _active.OnSubmit += HandleSubmit;
        _active.OnCancel += HandleCancel;

        root.AddChild(_active);
        _active.Attach(entity);

        var selected = channel ?? ResolveDefaultChannel();
        _active.InputBox.ChannelSelector.Select(selected);
        // Select() is a no-op when the target channel already matches the
        // freshly-constructed default, so the label can be blank. Force a
        // repaint of the button text regardless.
        _active.InputBox.ChannelSelector.UpdateChannelSelectButton(selected, null);

        // Make the widget a modal so Escape (CloseModals) and clicks outside
        // dismiss it without needing the LineEdit to hold keyboard focus.
        UIManager.PushModal(_active);

        _active.FocusInput();
    }

    private ChatSelectChannel ResolveDefaultChannel()
    {
        if (!_config.GetCVar(CCVars.FloatingChatInputRememberChannel))
            return ChatSelectChannel.Local;

        var stored = (ChatSelectChannel) _config.GetCVar(CCVars.FloatingChatInputLastChannel);
        var chatUi = UIManager.GetUIController<ChatUIController>();
        // Only restore if the channel is currently selectable (e.g. Dead is
        // gone once the player is alive again). Fall back to Local otherwise.
        if (stored != ChatSelectChannel.None && (chatUi.SelectableChannels & stored) != 0)
            return stored;

        return ChatSelectChannel.Local;
    }

    private void HandleSubmit(string text, ChatSelectChannel channel)
    {
        Close();

        if (string.IsNullOrWhiteSpace(text))
            return;

        var chatUi = UIManager.GetUIController<ChatUIController>();
        (var prefixChannel, text, _) = chatUi.SplitInputContents(text);

        if (prefixChannel != ChatSelectChannel.None)
            channel = prefixChannel;
        else if (channel == ChatSelectChannel.Radio)
        {
            // Radio routes through `say` with the common-radio prefix.
            text = $";{text}";
        }

        if (_config.GetCVar(CCVars.FloatingChatInputRememberChannel))
            _config.SetCVar(CCVars.FloatingChatInputLastChannel, (int) channel);

        _chatManager.SendMessage(text, channel);
    }

    private void HandleCancel()
    {
        Close();
    }

    public void Close()
    {
        if (_active == null)
            return;

        var widget = _active;
        _active = null;

        // Detach first so the ModalRemoved callback the engine fires while
        // tearing down has no subscribers left to re-enter Close(). Orphan
        // pops the widget off the modal stack for us.
        widget.OnSubmit -= HandleSubmit;
        widget.OnCancel -= HandleCancel;
        widget.InputBox.Input.ReleaseKeyboardFocus();
        widget.Orphan();
    }
}
