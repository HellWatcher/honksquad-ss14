namespace Content.Shared.Chat;

/// <summary>
/// Raised on a speaking entity before gathering chat recipients.
/// Subscribers can modify the voice range.
/// </summary>
[ByRefEvent]
public record struct GetVoiceRangeEvent(float Range);
