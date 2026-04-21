using Content.Shared.RussStation.Popups;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Shared.Popups;

// HONK - Fork partial adding a PopupCategory-carrying overload for every public Popup* method
// on SharedPopupSystem. Each overload forwards to the existing upstream method and broadcasts
// a local CategorizedPopupRaisedEvent so downstream fork systems (popup log, chat router,
// coalescer) can observe the category without touching each upstream call site. Implemented
// non-abstract so the fork does not have to patch the client and server subclasses as well.
// See issue #578 for the full popup categorization roadmap.
public abstract partial class SharedPopupSystem
{
    private void RaiseCategorized(string? message, PopupCategory category, PopupType type, EntityUid? source)
    {
        RaiseLocalEvent(new CategorizedPopupRaisedEvent(message, category, type, source));
    }

    public void PopupCursor(string? message, PopupCategory category, PopupType type = PopupType.Small)
    {
        PopupCursor(message, type);
        RaiseCategorized(message, category, type, null);
    }

    public void PopupCursor(string? message, ICommonSession recipient, PopupCategory category, PopupType type = PopupType.Small)
    {
        PopupCursor(message, recipient, type);
        RaiseCategorized(message, category, type, null);
    }

    public void PopupCursor(string? message, EntityUid recipient, PopupCategory category, PopupType type = PopupType.Small)
    {
        PopupCursor(message, recipient, type);
        RaiseCategorized(message, category, type, recipient);
    }

    public void PopupPredictedCursor(string? message, ICommonSession recipient, PopupCategory category, PopupType type = PopupType.Small)
    {
        PopupPredictedCursor(message, recipient, type);
        RaiseCategorized(message, category, type, null);
    }

    public void PopupPredictedCursor(string? message, EntityUid recipient, PopupCategory category, PopupType type = PopupType.Small)
    {
        PopupPredictedCursor(message, recipient, type);
        RaiseCategorized(message, category, type, recipient);
    }

    public void PopupCoordinates(string? message, EntityCoordinates coordinates, PopupCategory category, PopupType type = PopupType.Small)
    {
        PopupCoordinates(message, coordinates, type);
        RaiseCategorized(message, category, type, null);
    }

    public void PopupCoordinates(string? message, EntityCoordinates coordinates, Filter filter, bool recordReplay, PopupCategory category, PopupType type = PopupType.Small)
    {
        PopupCoordinates(message, coordinates, filter, recordReplay, type);
        RaiseCategorized(message, category, type, null);
    }

    public void PopupCoordinates(string? message, EntityCoordinates coordinates, EntityUid recipient, PopupCategory category, PopupType type = PopupType.Small)
    {
        PopupCoordinates(message, coordinates, recipient, type);
        RaiseCategorized(message, category, type, recipient);
    }

    public void PopupCoordinates(string? message, EntityCoordinates coordinates, ICommonSession recipient, PopupCategory category, PopupType type = PopupType.Small)
    {
        PopupCoordinates(message, coordinates, recipient, type);
        RaiseCategorized(message, category, type, null);
    }

    public void PopupPredictedCoordinates(string? message, EntityCoordinates coordinates, EntityUid? recipient, PopupCategory category, PopupType type = PopupType.Small)
    {
        PopupPredictedCoordinates(message, coordinates, recipient, type);
        RaiseCategorized(message, category, type, recipient);
    }

    public void PopupEntity(string? message, EntityUid uid, PopupCategory category, PopupType type = PopupType.Small)
    {
        PopupEntity(message, uid, type);
        RaiseCategorized(message, category, type, uid);
    }

    public void PopupEntity(string? message, EntityUid uid, EntityUid recipient, PopupCategory category, PopupType type = PopupType.Small)
    {
        PopupEntity(message, uid, recipient, type);
        RaiseCategorized(message, category, type, uid);
    }

    public void PopupEntity(string? message, EntityUid uid, ICommonSession recipient, PopupCategory category, PopupType type = PopupType.Small)
    {
        PopupEntity(message, uid, recipient, type);
        RaiseCategorized(message, category, type, uid);
    }

    public void PopupEntity(string? message, EntityUid uid, Filter filter, bool recordReplay, PopupCategory category, PopupType type = PopupType.Small)
    {
        PopupEntity(message, uid, filter, recordReplay, type);
        RaiseCategorized(message, category, type, uid);
    }

    public void PopupClient(string? message, EntityUid? recipient, PopupCategory category, PopupType type = PopupType.Small)
    {
        PopupClient(message, recipient, type);
        RaiseCategorized(message, category, type, recipient);
    }

    public void PopupClient(string? message, EntityUid uid, EntityUid? recipient, PopupCategory category, PopupType type = PopupType.Small)
    {
        PopupClient(message, uid, recipient, type);
        RaiseCategorized(message, category, type, uid);
    }

    public void PopupClient(string? message, EntityCoordinates coordinates, EntityUid? recipient, PopupCategory category, PopupType type = PopupType.Small)
    {
        PopupClient(message, coordinates, recipient, type);
        RaiseCategorized(message, category, type, recipient);
    }

    public void PopupPredicted(string? message, EntityUid uid, EntityUid? recipient, PopupCategory category, PopupType type = PopupType.Small)
    {
        PopupPredicted(message, uid, recipient, type);
        RaiseCategorized(message, category, type, uid);
    }

    public void PopupPredicted(string? message, EntityUid uid, EntityUid? recipient, Filter filter, bool recordReplay, PopupCategory category, PopupType type = PopupType.Small)
    {
        PopupPredicted(message, uid, recipient, filter, recordReplay, type);
        RaiseCategorized(message, category, type, uid);
    }

    public void PopupPredicted(string? recipientMessage, string? othersMessage, EntityUid uid, EntityUid? recipient, PopupCategory category, PopupType type = PopupType.Small)
    {
        PopupPredicted(recipientMessage, othersMessage, uid, recipient, type);
        RaiseCategorized(recipientMessage ?? othersMessage, category, type, uid);
    }
}
