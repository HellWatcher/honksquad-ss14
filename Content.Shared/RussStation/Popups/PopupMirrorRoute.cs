namespace Content.Shared.RussStation.Popups;

/// <summary>
/// Where a popup is mirrored in addition to the floating display.
/// Stored as int in the per-category CVars so users can tweak via server config without an enum mapping layer.
/// </summary>
public enum PopupMirrorRoute
{
    Disabled = 0,
    PopupTab = 1,
    MainChat = 2,
}
