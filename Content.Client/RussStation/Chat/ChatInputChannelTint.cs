// HONK - Tint the chat input's border with the active channel color so the
// player has a peripheral cue for which channel Enter will send to. Covers
// both the anchored chat panel and the floating input from #577. Issue #581.

using Content.Client.UserInterface.Systems.Chat.Controls;
using Content.Shared.Chat;
using Content.Shared.Radio;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.RussStation.Chat;

public static class ChatInputChannelTint
{
    private const int BorderPx = 2;
    private const float BorderAlpha = 0.85f;

    /// <summary>
    /// Applies a channel-colored border to the <see cref="ChatInputBox"/> that
    /// owns <paramref name="button"/>. No-op when the button isn't parented to
    /// a ChatInputBox yet.
    /// </summary>
    public static void Apply(ChannelSelectorButton button, ChatSelectChannel channel, RadioChannelPrototype? radio)
    {
        var inputBox = FindChatInputBox(button);
        if (inputBox == null)
            return;

        var color = (radio?.Color ?? button.ChannelSelectColor(channel)).WithAlpha(BorderAlpha);

        var style = EnsureStyleBox(inputBox);
        style.BorderColor = color;
        style.BorderThickness = new Thickness(BorderPx);
    }

    private static StyleBoxFlat EnsureStyleBox(ChatInputBox inputBox)
    {
        if (inputBox.PanelOverride is StyleBoxFlat existing)
            return existing;

        // Clone the resolved sheet panel so the override keeps the themed
        // background; only the border is our change. Fall back to transparent
        // when the sheet doesn't supply a StyleBoxFlat (e.g. texture-based
        // panel in a future theme).
        StyleBoxFlat clone;
        if (inputBox.TryGetStyleProperty<StyleBox>(PanelContainer.StylePropertyPanel, out var sheetBox)
            && sheetBox is StyleBoxFlat sheetFlat)
        {
            clone = new StyleBoxFlat(sheetFlat);
        }
        else
        {
            clone = new StyleBoxFlat(Color.Transparent);
        }

        inputBox.PanelOverride = clone;
        return clone;
    }

    private static ChatInputBox? FindChatInputBox(Control? control)
    {
        while (control != null)
        {
            if (control is ChatInputBox input)
                return input;
            control = control.Parent;
        }
        return null;
    }
}
