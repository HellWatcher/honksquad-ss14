// HONK — Owns the floating chat input widget lifecycle. See issue #577.

using Content.Client.Chat.Managers;
using Content.Client.UserInterface.Systems.Chat;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.Radio;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

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
    [Dependency] private readonly IPrototypeManager _protoManager = default!;

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
        RadioChannelPrototype? pendingRadio = null;
        if (selected == ChatSelectChannel.Radio
            && _config.GetCVar(CCVars.FloatingChatInputRememberChannel))
        {
            var lastRadioId = _config.GetCVar(CCVars.FloatingChatInputLastRadioChannel);
            if (!string.IsNullOrEmpty(lastRadioId)
                && _protoManager.TryIndex<RadioChannelPrototype>(lastRadioId, out var proto))
            {
                pendingRadio = proto;
            }
        }

        _active.RestoreChannel(selected, pendingRadio);

        // Make the widget a modal so Escape (CloseModals) and clicks outside
        // dismiss it without needing the LineEdit to hold keyboard focus.
        UIManager.PushModal(_active);

        _active.FocusInput();
    }

    private ChatSelectChannel ResolveDefaultChannel()
    {
        var chatUi = UIManager.GetUIController<ChatUIController>();
        return FloatingChatInputRouting.ResolveDefaultChannel(
            _config.GetCVar(CCVars.FloatingChatInputRememberChannel),
            _config.GetCVar(CCVars.FloatingChatInputLastChannel),
            chatUi.SelectableChannels);
    }

    private void HandleSubmit(string text, ChatSelectChannel channel)
    {
        // Snapshot the pending radio before Close() tears the widget down.
        var widgetPendingRadio = _active?.PendingRadioChannel;
        Close();

        if (string.IsNullOrWhiteSpace(text))
            return;

        var chatUi = UIManager.GetUIController<ChatUIController>();
        var (prefixChannel, strippedText, prefixRadio) = chatUi.SplitInputContents(text);

        RadioChannelPrototype? effectiveRadio = null;
        if (prefixChannel != ChatSelectChannel.None)
        {
            channel = prefixChannel;
            effectiveRadio = prefixRadio;
            text = strippedText;
        }
        else if (channel == ChatSelectChannel.Radio)
        {
            // No typed prefix — route via the restored pending radio channel if
            // we still have one, otherwise fall back to common radio.
            effectiveRadio = widgetPendingRadio;
            text = FloatingChatInputRouting.BuildRadioPrefixedText(text, effectiveRadio?.KeyCode);
        }

        if (_config.GetCVar(CCVars.FloatingChatInputRememberChannel))
        {
            _config.SetCVar(CCVars.FloatingChatInputLastChannel, (int) channel);
            _config.SetCVar(
                CCVars.FloatingChatInputLastRadioChannel,
                channel == ChatSelectChannel.Radio && effectiveRadio != null ? effectiveRadio.ID : string.Empty);
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
