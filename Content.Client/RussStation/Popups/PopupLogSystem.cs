using Content.Client.UserInterface.Systems.Chat;
using Content.Shared.Chat;
using Content.Shared.Examine;
using Content.Shared.Popups;
using Content.Shared.RussStation.Popups;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.Utility;

namespace Content.Client.RussStation.Popups;

/// <summary>
/// Mirrors every popup the local client sees into a dedicated chat channel so players can scroll back
/// through text that otherwise disappears with the floating display.
/// </summary>
/// <remarks>
/// Popups for entities the local player can't examine (out of range, occluded, wrong map) are dropped
/// so the log doesn't leak information the player shouldn't have. Cursor / coordinate-only popups with
/// no source entity always log since those are typically addressed directly to the local player.
/// </remarks>
public sealed class PopupLogSystem : EntitySystem
{
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;

    private ChatUIController? _chat;
    private ChatUIController Chat => _chat ??= _ui.GetUIController<ChatUIController>();

    public override void Initialize()
    {
        base.Initialize();
        // Single source: the client-side PopupSystem raises a CategorizedPopupRaisedEvent from
        // PopupMessage / PopupCursorInternal (HONK blocks), so network-sent popups, client-predicted
        // popups, and fork categorized calls all land here exactly once.
        SubscribeLocalEvent<CategorizedPopupRaisedEvent>(OnCategorizedPopup);
    }

    private void OnCategorizedPopup(CategorizedPopupRaisedEvent ev)
    {
        // Filter out popups the player shouldn't be able to perceive. Server PVS may ship popups
        // for entities outside the player's line of sight (proximity filters, broad broadcast);
        // gate those behind the same visibility rule the examine system uses. Self-sourced popups
        // (combat mode, spit, ActionGun text) and popups that don't carry an entity source always
        // log since those are directly addressed to the local player.
        if (ev.Source is { } sourceUid
            && _player.LocalEntity is { } examiner
            && sourceUid != examiner
            && !_examine.CanExamine(examiner, sourceUid))
        {
            return;
        }

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
