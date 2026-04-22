// HONK — pure helpers for the floating chat input. Extracted so the
// decision logic can be unit-tested without spinning up the widget.

using Content.Shared.Chat;

namespace Content.Client.RussStation.Chat;

public static class FloatingChatInputRouting
{
    /// <summary>
    /// What the channel selector button should display, given the current
    /// selector state, whether a restored radio channel is still pending,
    /// and the result of parsing a typed prefix.
    /// </summary>
    public enum LabelSource
    {
        /// <summary>Paint the currently-selected channel as-is.</summary>
        Selected,

        /// <summary>Paint the channel derived from the typed prefix.</summary>
        Prefix,

        /// <summary>Paint the pending (restored) radio channel.</summary>
        PendingRadio,
    }

    /// <summary>
    /// Build the prefixed text a <see cref="ChatSelectChannel.Radio"/> submit
    /// should send when the user did not type a prefix themselves. Uses the
    /// pending radio channel's keycode when one is known, otherwise common.
    /// </summary>
    public static string BuildRadioPrefixedText(string text, char? pendingKeycode)
    {
        if (pendingKeycode is { } kc && kc != '\0')
            return $"{SharedChatSystem.RadioChannelPrefix}{kc} {text}";

        return $"{SharedChatSystem.RadioCommonPrefix}{text}";
    }

    /// <summary>
    /// Decide which channel to seed the floating input with at open time,
    /// honouring the remember-channel toggle and the currently selectable
    /// mask so we never restore a channel the player lost access to.
    /// </summary>
    public static ChatSelectChannel ResolveDefaultChannel(
        bool rememberEnabled,
        int storedRaw,
        ChatSelectChannel selectable)
    {
        if (!rememberEnabled)
            return ChatSelectChannel.Local;

        var stored = (ChatSelectChannel) storedRaw;
        if (stored != ChatSelectChannel.None && (selectable & stored) != 0)
            return stored;

        return ChatSelectChannel.Local;
    }

    /// <summary>
    /// Pick which "thing" the channel selector label should render. Keeps
    /// the upstream <see cref="Controls.ChannelSelectorButton.Select"/>
    /// same-channel early-return bug (empty Text after construction) from
    /// biting by making the paint path unconditional on our side.
    /// </summary>
    public static LabelSource ResolveLabelSource(
        ChatSelectChannel selected,
        bool hasPendingRadio,
        ChatSelectChannel prefixChannel)
    {
        if (prefixChannel != ChatSelectChannel.None)
            return LabelSource.Prefix;

        if (selected == ChatSelectChannel.Radio && hasPendingRadio)
            return LabelSource.PendingRadio;

        return LabelSource.Selected;
    }
}
