using Content.Client.UserInterface.Systems.Chat;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.Popups;
using Content.Shared.RussStation.Popups;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Maths;
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
/// Repeated popups within a short window are coalesced in-place by ChatUIController (shared path with
/// emote coalescing), so this system does not need its own dedup dictionary.
/// </remarks>
public sealed class PopupLogSystem : EntitySystem
{
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;

    // Matches SharedChatSystem.VoiceRange so popups log at roughly the same radius as local speech,
    // instead of the 16-tile examine range that lets popups two screens away into the log.
    private const float LogRange = SharedChatSystem.VoiceRange;

    private const int MinFontSize = 8;
    private const int MaxFontSize = 16;
    private const string FallbackColor = "#9999aa";

    private string _style = "normal";
    private int _fontSize;
    private string _color = FallbackColor;

    private ChatUIController? _chat;
    private ChatUIController Chat => _chat ??= _ui.GetUIController<ChatUIController>();

    public override void Initialize()
    {
        base.Initialize();
        // Single source: the client-side PopupSystem raises a CategorizedPopupRaisedEvent from
        // PopupMessage / PopupCursorInternal (HONK blocks), so network-sent popups, client-predicted
        // popups, and fork categorized calls all land here exactly once.
        SubscribeLocalEvent<CategorizedPopupRaisedEvent>(OnCategorizedPopup);

        _style = SanitizeStyle(_config.GetCVar(CCVars.PopupLogStyle));
        _fontSize = ClampFontSize(_config.GetCVar(CCVars.PopupLogFontSize));
        _color = SanitizeColor(_config.GetCVar(CCVars.PopupLogColor));

        _config.OnValueChanged(CCVars.PopupLogStyle, v => _style = SanitizeStyle(v));
        _config.OnValueChanged(CCVars.PopupLogFontSize, v => _fontSize = ClampFontSize(v));
        _config.OnValueChanged(CCVars.PopupLogColor, v => _color = SanitizeColor(v));
    }

    public override void Shutdown()
    {
        // OnValueChanged handlers are weak via the lambdas, but the controller cleans them up
        // anyway when the system is destroyed; nothing to do here yet beyond the base call.
        base.Shutdown();
    }

    private static int ClampFontSize(int v) => Math.Clamp(v, MinFontSize, MaxFontSize);

    private static string SanitizeStyle(string raw) => raw switch
    {
        "italic" => "italic",
        "bold" => "bold",
        "bold-italic" => "bold-italic",
        _ => "normal",
    };

    private static string SanitizeColor(string raw)
    {
        // Trip-wire against malformed user input: an unparseable color would corrupt the rich
        // text. Fall back to the original dim grey if Color.TryFromHex rejects it.
        var trimmed = raw.Trim();
        return Color.TryFromHex(trimmed) is null ? FallbackColor : trimmed;
    }

    private void OnCategorizedPopup(CategorizedPopupRaisedEvent ev)
    {
        // Drop popups whose source is further than local-chat range. Server PVS ships popups for
        // entities well outside the player's chat radius (proximity filters, broad broadcast), and
        // the previous examine-range gate (16 tiles) was wider than VoiceRange (10 tiles), so the
        // popup tab filled with text from people two screens over. Self-sourced popups and popups
        // without an entity source always log since those are addressed to the local player.
        if (ev.Source is { } sourceUid
            && _player.LocalEntity is { } examiner
            && sourceUid != examiner
            && !_transform.InRange(examiner, sourceUid, LogRange))
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
        var body = category is { } cat
            ? $"\\[{cat}\\] {escaped}"
            : escaped;

        // Apply user-tunable formatting last so all three knobs compose: color wraps the line,
        // style (italic/bold) wraps the colored line, font size wraps everything. Default values
        // reproduce the pre-cvar look (dim grey prefix, no style, default size).
        var wrapped = $"[color={_color}]{body}[/color]";
        if (_style == "italic" || _style == "bold-italic")
            wrapped = $"[italic]{wrapped}[/italic]";
        if (_style == "bold" || _style == "bold-italic")
            wrapped = $"[bold]{wrapped}[/bold]";
        wrapped = $"[font size={_fontSize}]{wrapped}[/font]";

        var mirror = new ChatMessage(
            ChatChannel.Popup,
            message,
            wrapped,
            source,
            senderKey: null);

        Chat.ProcessChatMessage(mirror, speechBubble: false);
    }
}
