using Content.Client.UserInterface.Systems.Chat;
using Content.Shared.Chat;
using Content.Shared.Popups;
using Content.Shared.RussStation.Popups;
using Robust.Client.UserInterface;
using Robust.Shared.Utility;

namespace Content.Client.RussStation.Popups;

/// <summary>
/// Mirrors every popup the local client sees into a dedicated chat channel so players can scroll back
/// through text that otherwise disappears with the floating display.
/// </summary>
/// <remarks>
/// Subscribes to the three popup network events plus the fork's <see cref="CategorizedPopupRaisedEvent"/>.
/// Category-tagged mirrors only appear for fork-side categorized calls; server-originated popups arrive
/// here through the network path and are logged without a category. A future PR will network the category
/// alongside the popup to close that gap.
/// </remarks>
public sealed class PopupLogSystem : EntitySystem
{
    [Dependency] private readonly IUserInterfaceManager _ui = default!;

    private ChatUIController? _chat;
    private ChatUIController Chat => _chat ??= _ui.GetUIController<ChatUIController>();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<PopupCursorEvent>(OnPopupCursor);
        SubscribeNetworkEvent<PopupCoordinatesEvent>(OnPopupCoordinates);
        SubscribeNetworkEvent<PopupEntityEvent>(OnPopupEntity);
        SubscribeLocalEvent<CategorizedPopupRaisedEvent>(OnCategorizedPopup);
    }

    private void OnPopupCursor(PopupCursorEvent ev)
    {
        LogMirroredPopup(ev.Message, NetEntity.Invalid, null);
    }

    private void OnPopupCoordinates(PopupCoordinatesEvent ev)
    {
        LogMirroredPopup(ev.Message, NetEntity.Invalid, null);
    }

    private void OnPopupEntity(PopupEntityEvent ev)
    {
        LogMirroredPopup(ev.Message, ev.Uid, null);
    }

    private void OnCategorizedPopup(CategorizedPopupRaisedEvent ev)
    {
        var source = ev.Source is { } uid ? GetNetEntity(uid) : NetEntity.Invalid;
        LogMirroredPopup(ev.Message, source, ev.Category);
    }

    private void LogMirroredPopup(string? message, NetEntity source, PopupCategory? category)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        var escaped = FormattedMessage.EscapeText(message);
        var wrapped = category is { } cat
            ? $"[color=#9999aa]\\[{cat}\\][/color] {escaped}"
            : escaped;

        var mirror = new ChatMessage(
            ChatChannel.Popup,
            message,
            wrapped,
            source,
            senderKey: null);

        Chat.ProcessChatMessage(mirror, speechBubble: false);
    }
}
