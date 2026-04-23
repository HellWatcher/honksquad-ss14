using Content.Shared.Chat;
using Robust.Shared.Utility;

namespace Content.Client.Chat.UI;

// HONK - Fork partial adding repeat-in-place support for speech bubbles, mirroring the
// PopupSystem.WrapAndRepeatPopup behavior. When an alive bubble gets another copy of the same
// emote from the same entity inside the coalesce window, ChatUIController calls RepeatWith on
// the existing bubble instead of spawning a stacked one. Only emote bubbles are coalesced today;
// say/whisper/looc keep one-bubble-per-line since repeats there are rare and usually carry
// meaningful turn-taking.
public abstract partial class SpeechBubble
{
    // Snapshots taken at construction so a later mutation of the source ChatMessage (e.g. the
    // chat coalescer rewriting WrappedMessage to "(xN)") can't leak into a rebuild and produce
    // double-wrapped counts.
    private string _honkOriginalRawMessage = string.Empty;
    private string _honkOriginalWrapped = string.Empty;
    private ChannelMetadata _honkMetadata;
    private string _honkSpeechStyleClass = string.Empty;
    private Color? _honkFontColor;

    /// <summary>
    /// Raw message text of the first bubble in the current repeat cluster. Used by ChatUIController
    /// to match incoming emotes against alive bubbles.
    /// </summary>
    public string OriginalMessageText => _honkOriginalRawMessage;

    /// <summary>
    /// Number of repeats this bubble currently displays (1 for a fresh bubble).
    /// </summary>
    public int Repeats { get; private set; } = 1;

    private void HonkStashCtorArgs(ChatMessage message, string speechStyleClass, Color? fontColor)
    {
        _honkOriginalRawMessage = message.Message;
        _honkOriginalWrapped = message.WrappedMessage;
        _honkMetadata = new ChannelMetadata(message.Channel, message.SenderEntity, message.SenderKey);
        _honkSpeechStyleClass = speechStyleClass;
        _honkFontColor = fontColor;
    }

    private readonly record struct ChannelMetadata(ChatChannel Channel, NetEntity Sender, int? SenderKey);

    /// <summary>
    /// Fold another identical emote into this bubble: bump the repeat count, rebuild the rendered
    /// text with an (xN) suffix using the same loc key PopupSystem uses, and reset the death timer
    /// so the bubble stays visible while repeats keep arriving.
    /// </summary>
    public void RepeatWith(ChatMessage newMessage)
    {
        Repeats += 1;

        var display = new ChatMessage(
            _honkMetadata.Channel,
            _honkOriginalRawMessage,
            Loc.GetString(
                "popup-system-repeated-popup-stacking-wrap",
                ("popup-message", _honkOriginalWrapped),
                ("count", Repeats)),
            _honkMetadata.Sender,
            _honkMetadata.SenderKey);

        RemoveAllChildren();
        var rebuilt = BuildBubble(display, _honkSpeechStyleClass, _honkFontColor);
        AddChild(rebuilt);
        ForceRunStyleUpdate();
        rebuilt.Measure(Vector2Helpers.Infinity);
        ContentSize = rebuilt.DesiredSize;
        _deathTime = _timing.RealTime + TotalTime;
    }
}
