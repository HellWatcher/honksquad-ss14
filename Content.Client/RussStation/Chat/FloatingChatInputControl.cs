// HONK — Floating chat input widget anchored above the local player entity.
// Positioning mirrors Content.Client/Chat/UI/SpeechBubble.cs. See issue #577.

using System.Numerics;
using Content.Client.UserInterface.Systems.Chat.Controls;
using Content.Shared.Chat;
using Content.Shared.Input;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
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

    private readonly SharedTransformSystem _transform;

    /// <summary>World-space vertical offset applied above the entity's origin.</summary>
    private const float EntityVerticalOffset = 0.6f;

    public readonly ChatInputBox InputBox;

    private EntityUid _anchorEntity;
    private Vector2 _contentSize;

    public event Action<string, ChatSelectChannel>? OnSubmit;
    public event Action? OnCancel;

    public FloatingChatInputControl()
    {
        IoCManager.InjectDependencies(this);
        _transform = _entManager.System<SharedTransformSystem>();

        InputBox = new ChatInputBox
        {
            MinWidth = 320,
        };
        // Channel filter button is noise for an ephemeral floating input.
        InputBox.FilterButton.Visible = false;
        AddChild(InputBox);

        InputBox.Input.OnTextEntered += OnTextEntered;
        InputBox.Input.OnKeyBindDown += OnInputKeyBindDown;
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

        var offset = (-_eyeManager.CurrentEye.Rotation).ToWorldVec() * -EntityVerticalOffset;
        var worldPos = _transform.GetWorldPosition(xform) + offset;

        var anchor = _eyeManager.WorldToScreen(worldPos) / UIScale;
        var screenPos = anchor - new Vector2(_contentSize.X / 2f, _contentSize.Y);
        screenPos = (screenPos * 2).Rounded() / 2;
        LayoutContainer.SetPosition(this, screenPos);
    }

}
