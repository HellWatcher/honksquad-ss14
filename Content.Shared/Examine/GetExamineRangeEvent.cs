namespace Content.Shared.Examine;

/// <summary>
/// Raised on an examining entity before calculating examine range.
/// Subscribers can modify the range.
/// </summary>
[ByRefEvent]
public record struct GetExamineRangeEvent(float Range);
