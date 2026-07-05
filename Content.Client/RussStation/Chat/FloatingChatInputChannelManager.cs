// HONK — channel state + label refresh for the floating chat input.
// Extracted from FloatingChatInputControl (issue #863) so the widget keeps
// lifecycle/positioning concerns while this owns channel selection. Pure
// routing decisions still live in FloatingChatInputRouting; this wires them
// to the live ChatInputBox.

using Content.Client.UserInterface.Systems.Chat;
using Content.Client.UserInterface.Systems.Chat.Controls;
using Content.Shared.Chat;
using Content.Shared.Radio;
using Robust.Shared.Maths;

namespace Content.Client.RussStation.Chat;

/// <summary>
/// Owns the channel-selection state for <see cref="FloatingChatInputControl"/>:
/// the pending restored radio channel, dropdown/cycle interactions, and the
/// selector button label refresh.
/// </summary>
public sealed class FloatingChatInputChannelManager
{
    private readonly ChatInputBox _inputBox;
    private readonly ChatUIController _chatUi;

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

    public FloatingChatInputChannelManager(ChatInputBox inputBox, ChatUIController chatUi)
    {
        _inputBox = inputBox;
        _chatUi = chatUi;
    }

    /// <summary>
    /// Handler for <see cref="ChannelSelectorButton.OnChannelSelect"/>. User
    /// interaction (dropdown or cycle) overrides any restored radio target.
    /// Programmatic restore via <see cref="RestoreChannel"/> suppresses this.
    /// </summary>
    public void OnChannelSelectorChanged(ChatSelectChannel channel)
    {
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
            _inputBox.ChannelSelector.Select(channel);
        }
        finally
        {
            _suppressPendingClear = false;
        }

        _pendingRadioChannel = channel == ChatSelectChannel.Radio ? pendingRadio : null;
        RefreshChannelLabel();
    }

    /// <summary>
    /// Mirrors <see cref="ChatUIController.UpdateSelectedChannel"/>. The button
    /// text is only refreshed here; <see cref="ChannelSelectorButton.Select"/>
    /// short-circuits on same-channel and neither it nor the dropdown's
    /// OnChannelSelect handler repaints the label.
    /// </summary>
    public void RefreshChannelLabel()
    {
        var (prefixChannel, _, prefixRadio) = _chatUi.SplitInputContents(_inputBox.Input.Text.ToLower());
        var selected = _inputBox.ChannelSelector.SelectedChannel;

        var source = FloatingChatInputRouting.ResolveLabelSource(
            selected,
            _pendingRadioChannel != null,
            prefixChannel);

        switch (source)
        {
            case FloatingChatInputRouting.LabelSource.Prefix:
                _inputBox.ChannelSelector.UpdateChannelSelectButton(prefixChannel, prefixRadio);
                break;
            case FloatingChatInputRouting.LabelSource.PendingRadio:
                _inputBox.ChannelSelector.UpdateChannelSelectButton(ChatSelectChannel.Radio, _pendingRadioChannel);
                break;
            default:
                _inputBox.ChannelSelector.UpdateChannelSelectButton(selected, null);
                break;
        }
    }

    public void CycleChannel(bool forward)
    {
        var order = ChannelSelectorPopup.ChannelSelectorOrder;
        var idx = Array.IndexOf(order, _inputBox.ChannelSelector.SelectedChannel);
        do
        {
            idx += forward ? 1 : -1;
            idx = MathHelper.Mod(idx, order.Length);
        } while ((_chatUi.SelectableChannels & order[idx]) == 0);

        var target = _chatUi.MapLocalIfGhost(order[idx]);
        if ((_chatUi.SelectableChannels & target) == 0)
            return;

        _inputBox.ChannelSelector.Select(target);
    }
}
