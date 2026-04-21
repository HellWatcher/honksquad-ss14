namespace Content.Shared.RussStation.Popups;

/// <summary>
/// Broadcast locally when a categorized popup is raised through the fork overloads on <see cref="Content.Shared.Popups.SharedPopupSystem"/>.
/// Consumers (popup log, chat router, coalescer) subscribe to this instead of intercepting each <c>Popup*</c> overload.
/// </summary>
/// <remarks>
/// This is a local broadcast, not a networked event. A categorized popup raised on the server does not automatically
/// reach the client through this event; the client receives the underlying <c>PopupEvent</c> via the existing upstream path.
/// Downstream PRs add networked category plumbing once the log UI exists to consume it.
/// </remarks>
public sealed class CategorizedPopupRaisedEvent : EntityEventArgs
{
    public string? Message { get; }
    public PopupCategory Category { get; }
    public Content.Shared.Popups.PopupType Type { get; }

    /// <summary>
    /// Source entity for the popup when one exists (PopupEntity / PopupClient / PopupPredicted variants).
    /// Null for cursor and coordinate variants.
    /// </summary>
    public EntityUid? Source { get; }

    public CategorizedPopupRaisedEvent(string? message, PopupCategory category, Content.Shared.Popups.PopupType type, EntityUid? source)
    {
        Message = message;
        Category = category;
        Type = type;
        Source = source;
    }
}
