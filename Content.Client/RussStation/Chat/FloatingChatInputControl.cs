// HONK — Floating chat input widget anchored above the local player entity.
// Positioning mirrors Content.Client/Chat/UI/SpeechBubble.cs. See issue #577.

using System.Numerics;
using Content.Client.UserInterface.Systems.Chat;
using Content.Client.UserInterface.Systems.Chat.Controls;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.Input;
using Content.Shared.Radio;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Input;
using Robust.Shared.Maths;
using Robust.Shared.Timing;
using static Robust.Client.UserInterface.Controls.LineEdit;

namespace Content.Client.RussStation.Chat;

/// <summary>
/// Floating chat input that follows the local player's sprite on screen. Created on demand by
/// <see cref="FloatingChatInputController"/> when the focus-chat keybind fires and the
/// <c>honk.chat.floating_input</c> CVar is enabled.
/// </summary>
public sealed partial class FloatingChatInputControl : Control
{
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private IEyeManager _eyeManager = default!;
    [Dependency] private IUserInterfaceManager _uiManager = default!;
    [Dependency] private IConfigurationManager _config = default!;

    private readonly SharedTransformSystem _transform;
    private readonly StyleBoxFlat _backgroundStyle;
    private readonly ChatUIController _chatUi;
    private readonly FloatingChatInputChannelManager _channelManager;

    public readonly ChatInputBox InputBox;

    private EntityUid _anchorEntity;
    private Vector2 _contentSize;

    public event Action<string, ChatSelectChannel>? OnSubmit;
    public event Action? OnCancel;

    /// <summary>
    /// Specific radio channel to route to when the widget submits on
    /// <see cref="ChatSelectChannel.Radio"/> without a typed prefix.
    /// </summary>
    public RadioChannelPrototype? PendingRadioChannel => _channelManager.PendingRadioChannel;

    public FloatingChatInputControl()
    {
        IoCManager.InjectDependencies(this);
        _transform = _entManager.System<SharedTransformSystem>();
        _chatUi = _uiManager.GetUIController<ChatUIController>();

        // Thicker background than the anchored chat panel — the floating
        // widget overlaps the game world, so a more opaque fill keeps typed
        // text legible. Share the accessibility "Speech bubble background
        // opacity" option rather than adding a duplicate slider; in-world
        // text surfaces belong to the same knob.
        _backgroundStyle = new StyleBoxFlat(BuildBackgroundColor(_config.GetCVar(CCVars.SpeechBubbleBackgroundOpacity)));

        InputBox = new ChatInputBox
        {
            MinWidth = FloatingChatInputConstants.InputMinWidth,
            PanelOverride = _backgroundStyle,
        };
        // Channel filter button is noise for an ephemeral floating input.
        InputBox.FilterButton.Visible = false;
        AddChild(InputBox);

        _channelManager = new FloatingChatInputChannelManager(InputBox, _chatUi);

        InputBox.Input.OnTextEntered += OnTextEntered;
        InputBox.Input.OnKeyBindDown += OnInputKeyBindDown;
        InputBox.Input.OnTextChanged += OnInputTextChanged;
        InputBox.Input.OnFocusEnter += OnInputFocusEnter;
        InputBox.Input.OnFocusExit += OnInputFocusExit;
        InputBox.ChannelSelector.OnChannelSelect += _channelManager.OnChannelSelectorChanged;

        // Pick up the accessibility text-opacity knob too so the typed text
        // fades in sync with in-world bubble text.
        ApplyTextOpacity(_config.GetCVar(CCVars.SpeechBubbleTextOpacity));

        _config.OnValueChanged(CCVars.SpeechBubbleBackgroundOpacity, ApplyBackgroundOpacity);
        _config.OnValueChanged(CCVars.SpeechBubbleTextOpacity, ApplyTextOpacity);
    }

    private void ApplyTextOpacity(float alpha)
        => InputBox.Input.ModulateSelfOverride = Color.White.WithAlpha(Math.Clamp(alpha, 0f, 1f));

    private void ApplyBackgroundOpacity(float alpha)
        => _backgroundStyle.BackgroundColor = BuildBackgroundColor(alpha);

    private static Color BuildBackgroundColor(float alpha)
    {
        return new Color(
            FloatingChatInputConstants.BackgroundRed,
            FloatingChatInputConstants.BackgroundGreen,
            FloatingChatInputConstants.BackgroundBlue).WithAlpha(Math.Clamp(alpha, 0f, 1f));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _config.UnsubValueChanged(CCVars.SpeechBubbleBackgroundOpacity, ApplyBackgroundOpacity);
            _config.UnsubValueChanged(CCVars.SpeechBubbleTextOpacity, ApplyTextOpacity);
            // Clear the typing indicator if the widget vanishes mid-message so it doesn't stick.
            _chatUi.NotifyChatFocus(false);
        }
    }

    /// <summary>
    /// Open-time channel seed. Selects the channel and, when it is Radio,
    /// stores the pending radio prototype so both the button label and
    /// submit routing address it.
    /// </summary>
    public void RestoreChannel(ChatSelectChannel channel, RadioChannelPrototype? pendingRadio)
        => _channelManager.RestoreChannel(channel, pendingRadio);

    private void OnInputTextChanged(LineEditEventArgs args)
    {
        _channelManager.RefreshChannelLabel();
        // Mirror ChatBox: the typing-indicator system listens for these notifications, so the
        // floating input has to call them too or bystanders never see the indicator while the
        // local player is typing here.
        _chatUi.NotifyChatTextChange();
    }

    private void OnInputFocusEnter(LineEditEventArgs args)
    {
        _chatUi.NotifyChatFocus(true);
    }

    private void OnInputFocusExit(LineEditEventArgs args)
    {
        _chatUi.NotifyChatFocus(false);
    }

    /// <summary>
    /// Engine invokes this when the widget is popped from the modal stack
    /// (Escape via CloseModals, or a click outside the widget). Route that
    /// back to the controller so it can tear down exactly once.
    /// </summary>
    protected override void ModalRemoved()
    {
        base.ModalRemoved();
        OnCancel?.Invoke();
    }

    public void Attach(EntityUid entity)
    {
        _anchorEntity = entity;
        Measure(Vector2Helpers.Infinity);
        _contentSize = DesiredSize;
    }

    public void FocusInput()
    {
        InputBox.Input.IgnoreNext = true;
        InputBox.Input.GrabKeyboardFocus();
    }

    private void OnTextEntered(LineEditEventArgs args)
    {
        var text = args.Text;
        var channel = (ChatSelectChannel) InputBox.ChannelSelector.SelectedChannel;
        OnSubmit?.Invoke(text, channel);
    }

    private void OnInputKeyBindDown(GUIBoundKeyEventArgs args)
    {
        if (args.Function == EngineKeyFunctions.TextReleaseFocus)
        {
            args.Handle();
            OnCancel?.Invoke();
            return;
        }

        if (args.Function == ContentKeyFunctions.CycleChatChannelForward)
        {
            _channelManager.CycleChannel(true);
            args.Handle();
            return;
        }

        if (args.Function == ContentKeyFunctions.CycleChatChannelBackward)
        {
            _channelManager.CycleChannel(false);
            args.Handle();
        }
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (!_entManager.TryGetComponent<TransformComponent>(_anchorEntity, out var xform)
            || xform.MapID != _eyeManager.CurrentEye.Position.MapId)
        {
            Visible = false;
            return;
        }

        Visible = true;

        // Recompute content size each frame in case the input resizes as the player types.
        Measure(Vector2Helpers.Infinity);
        _contentSize = DesiredSize;

        var offset = (-_eyeManager.CurrentEye.Rotation).ToWorldVec() * -FloatingChatInputConstants.EntityVerticalOffset;
        var worldPos = _transform.GetWorldPosition(xform) + offset;

        var anchor = _eyeManager.WorldToScreen(worldPos) / UIScale;
        var screenPos = anchor - new Vector2(_contentSize.X / 2f, _contentSize.Y);
        screenPos = (screenPos * 2).Rounded() / 2;
        LayoutContainer.SetPosition(this, screenPos);
    }

}
