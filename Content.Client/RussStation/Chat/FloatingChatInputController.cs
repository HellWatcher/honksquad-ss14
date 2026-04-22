// HONK — Owns the floating chat input widget lifecycle. See issue #577.

using Content.Client.Chat.Managers;
using Content.Client.UserInterface.Systems.Chat;
using Content.Shared.Chat;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;

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

        if (channel.HasValue)
            _active.InputBox.ChannelSelector.Select(channel.Value);

        _active.FocusInput();
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

        _active.OnSubmit -= HandleSubmit;
        _active.OnCancel -= HandleCancel;
        _active.InputBox.Input.ReleaseKeyboardFocus();
        _active.Orphan();
        _active = null;
    }
}
