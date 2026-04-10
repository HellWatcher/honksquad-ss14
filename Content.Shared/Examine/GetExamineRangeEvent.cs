namespace Content.Shared.Examine;

/// <summary>
/// Raised on the examining entity to determine effective examine range.
/// Allows components to modify how far the entity can examine.
/// </summary>
[ByRefEvent]
public record struct GetExamineRangeEvent(float Range);
