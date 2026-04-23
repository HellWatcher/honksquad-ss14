using Content.Client.Chat.UI;
using Content.Client.UserInterface.Systems.Chat;
using Content.Shared.Chat;
using Robust.Shared.Network;

namespace Content.Client.UserInterface.Systems.Chat;

// HONK - Fork partial adding in-place coalescing for repeated popup mirror and emote chat lines,
// plus repeat-in-place for floating emote speech bubbles. Mirrors upstream's PopupSystem
// WrapAndRepeatPopup behavior: if an identical message from the same sender lands inside a short
// window, mutate the existing rendered entry rather than appending a new line. Chat boxes get
// notified via MessageUpdated and patch the rendered OutputPanel entry in place. After the window
// lapses, the next identical message starts a new line at count 1.
public sealed partial class ChatUIController
{
    private static readonly TimeSpan HonkCoalesceWindow = TimeSpan.FromSeconds(2);

    // Channels whose repeats should collapse into a single rendered entry with an (xN) suffix.
    // Other channels keep one-entry-per-message behavior so e.g. two identical Local lines from
    // different speakers don't collapse visually.
    private static readonly HashSet<ChatChannel> HonkCoalesceChannels = new()
    {
        ChatChannel.Popup,
        ChatChannel.Emotes,
    };

    private readonly Dictionary<(ChatChannel Channel, NetEntity Sender, string Message), HonkChatCoalesceEntry> _honkCoalesce = new();

    /// <summary>
    /// Raised when an existing ChatMessage's WrappedMessage has been mutated in place because a
    /// matching repeat landed within the coalesce window. Subscribers should re-render that message
    /// without adding a new entry.
    /// </summary>
    public event Action<ChatMessage>? MessageUpdated;

    /// <summary>
    /// Returns true when <paramref name="msg"/> was folded into an existing rendered entry instead
    /// of producing a new one. The caller should skip History.Add / MessageAdded when this returns true.
    /// </summary>
    private bool HonkTryCoalesceChatMessage(ChatMessage msg)
    {
        if (!HonkCoalesceChannels.Contains(msg.Channel))
            return false;

        if (string.IsNullOrEmpty(msg.Message))
            return false;

        var now = _timing.RealTime;
        var key = (msg.Channel, msg.SenderEntity, msg.Message);

        if (_honkCoalesce.TryGetValue(key, out var entry) && now - entry.LastSeen < HonkCoalesceWindow)
        {
            entry.Repeats += 1;
            entry.LastSeen = now;
            entry.Msg.WrappedMessage = Loc.GetString(
                "popup-system-repeated-popup-stacking-wrap",
                ("popup-message", entry.OriginalWrapped),
                ("count", entry.Repeats));
            MessageUpdated?.Invoke(entry.Msg);
            return true;
        }

        _honkCoalesce[key] = new HonkChatCoalesceEntry
        {
            Msg = msg,
            OriginalWrapped = msg.WrappedMessage,
            Repeats = 1,
            LastSeen = now,
        };
        HonkPruneCoalesce(now);
        return false;
    }

    private void HonkPruneCoalesce(TimeSpan now)
    {
        if (_honkCoalesce.Count < 32)
            return;

        var stale = new List<(ChatChannel, NetEntity, string)>();
        foreach (var (key, entry) in _honkCoalesce)
        {
            if (now - entry.LastSeen >= HonkCoalesceWindow)
                stale.Add(key);
        }
        foreach (var key in stale)
            _honkCoalesce.Remove(key);
    }

    /// <summary>
    /// Returns true when an alive emote bubble for <paramref name="entity"/> already displays
    /// <paramref name="msg"/>'s text and was updated in place. Caller should skip creating a new bubble.
    /// </summary>
    private bool HonkTryCoalesceEmoteBubble(EntityUid entity, ChatMessage msg, SpeechBubble.SpeechType speechType)
    {
        if (speechType != SpeechBubble.SpeechType.Emote)
            return false;

        if (!_activeSpeechBubbles.TryGetValue(entity, out var bubbles) || bubbles.Count == 0)
            return false;

        // Match the most recent alive bubble only. Coalescing into an older bubble that has a newer
        // different bubble above it would visually reorder the stack.
        var candidate = bubbles[^1];
        if (candidate.OriginalMessageText != msg.Message)
            return false;

        candidate.RepeatWith(msg);
        return true;
    }

}
