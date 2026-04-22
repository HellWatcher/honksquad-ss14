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
public sealed class FloatingChatInputControl : Control
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly IUserInterfaceManager _uiManager = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;

    private readonly SharedTransformSystem _transform;
    private readonly StyleBoxFlat _backgroundStyle;

    public readonly ChatInputBox InputBox;

    private EntityUid _anchorEntity;
    private Vector2 _contentSize;

    public event Action<string, ChatSelectChannel>? OnSubmit;
    public event Action? OnCancel;

    private RadioChannelPrototype? _pendingRadioChannel;
    private bool _suppressPendingClear;

    /// <summary>
    /// Specific radio channel to route to when the widget submits on
    /// <see cref="ChatSelectChannel.Radio"/> without a typed prefix.
    /// Cleared automatically when the user picks a channel via dropdown
    /// or cycle hotkey; use <see cref="RestoreChannel"/> to seed it at
    /// open time.
    /// </summary>
    public RadioChannelPrototype? PendingRadioChannel => _pendingRadioChannel;

    public FloatingChatInputControl()
    {
        IoCManager.InjectDependencies(this);
        _transform = _entManager.System<SharedTransformSystem>();

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

        InputBox.Input.OnTextEntered += OnTextEntered;
        InputBox.Input.OnKeyBindDown += OnInputKeyBindDown;
        InputBox.Input.OnTextChanged += OnInputTextChanged;
        InputBox.ChannelSelector.OnChannelSelect += OnChannelSelectorChanged;

        // Pick up the accessibility text-opacity knob too so the typed text
        // fades in sync with in-world bubble text.
        ApplyTextOpacity(_config.GetCVar(CCVars.SpeechBubbleTextOpacity));

        _config.OnValueChanged(CCVars.SpeechBubbleBackgroundOpacity, OnBackgroundOpacityChanged);
        _config.OnValueChanged(CCVars.SpeechBubbleTextOpacity, OnTextOpacityChanged);
    }

    private void ApplyTextOpacity(float alpha)
    {
        InputBox.Input.ModulateSelfOverride = Color.White.WithAlpha(Math.Clamp(alpha, 0f, 1f));
    }

    private void OnTextOpacityChanged(float newAlpha)
    {
        ApplyTextOpacity(newAlpha);
    }

    private static Color BuildBackgroundColor(float alpha)
    {
        return new Color(
            FloatingChatInputConstants.BackgroundRed,
            FloatingChatInputConstants.BackgroundGreen,
            FloatingChatInputConstants.BackgroundBlue).WithAlpha(Math.Clamp(alpha, 0f, 1f));
    }

    private void OnBackgroundOpacityChanged(float newAlpha)
    {
        _backgroundStyle.BackgroundColor = BuildBackgroundColor(newAlpha);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _config.UnsubValueChanged(CCVars.SpeechBubbleBackgroundOpacity, OnBackgroundOpacityChanged);
            _config.UnsubValueChanged(CCVars.SpeechBubbleTextOpacity, OnTextOpacityChanged);
        }
    }

    private void OnChannelSelectorChanged(ChatSelectChannel channel)
    {
        // User interaction (dropdown or cycle) overrides any restored radio
        // target. Programmatic restore via RestoreChannel suppresses this.
        if (!_suppressPendingClear)
            _pendingRadioChannel = null;

        RefreshChannelLabel();
    }

    /// <summary>
    /// Open-time channel seed. Selects the channel and, when it is Radio,
    /// stores the pending radio prototype so both the button label and
    /// submit routing address it.
    /// </summary>
    public void RestoreChannel(ChatSelectChannel channel, RadioChannelPrototype? pendingRadio)
    {
        _suppressPendingClear = true;
        try
        {
            InputBox.ChannelSelector.Select(channel);
        }
        finally
        {
            _suppressPendingClear = false;
        }

        _pendingRadioChannel = channel == ChatSelectChannel.Radio ? pendingRadio : null;
        RefreshChannelLabel();
    }

    private void OnInputTextChanged(LineEditEventArgs args)
    {
        RefreshChannelLabel();
    }

    /// <summary>
    /// Mirrors <see cref="ChatUIController.UpdateSelectedChannel"/>. The button
    /// text is only refreshed here; <see cref="ChannelSelectorButton.Select"/>
    /// short-circuits on same-channel and neither it nor the dropdown's
    /// OnChannelSelect handler repaints the label.
    /// </summary>
    private void RefreshChannelLabel()
    {
        var chatUi = _uiManager.GetUIController<ChatUIController>();
        var (prefixChannel, _, prefixRadio) = chatUi.SplitInputContents(InputBox.Input.Text.ToLower());
        var selected = InputBox.ChannelSelector.SelectedChannel;

        var source = FloatingChatInputRouting.ResolveLabelSource(
            selected,
            _pendingRadioChannel != null,
            prefixChannel);

        switch (source)
        {
            case FloatingChatInputRouting.LabelSource.Prefix:
                InputBox.ChannelSelector.UpdateChannelSelectButton(prefixChannel, prefixRadio);
                break;
            case FloatingChatInputRouting.LabelSource.PendingRadio:
                InputBox.ChannelSelector.UpdateChannelSelectButton(ChatSelectChannel.Radio, _pendingRadioChannel);
                break;
            default:
                InputBox.ChannelSelector.UpdateChannelSelectButton(selected, null);
                break;
        }
    }

    private void CycleChannel(bool forward)
    {
        var chatUi = _uiManager.GetUIController<ChatUIController>();
        var order = ChannelSelectorPopup.ChannelSelectorOrder;
        var idx = Array.IndexOf(order, InputBox.ChannelSelector.SelectedChannel);
        do
        {
            idx += forward ? 1 : -1;
            idx = MathHelper.Mod(idx, order.Length);
        } while ((chatUi.SelectableChannels & order[idx]) == 0);

        var target = chatUi.MapLocalIfGhost(order[idx]);
        if ((chatUi.SelectableChannels & target) == 0)
            return;

        InputBox.ChannelSelector.Select(target);
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
            CycleChannel(true);
            args.Handle();
            return;
        }

        if (args.Function == ContentKeyFunctions.CycleChatChannelBackward)
        {
            CycleChannel(false);
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
