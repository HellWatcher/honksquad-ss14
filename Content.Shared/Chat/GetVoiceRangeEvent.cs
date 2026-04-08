namespace Content.Shared.Chat;

/// <summary>
/// Raised on the speaking entity to determine effective voice range.
/// Allows components to modify how far the entity's voice carries.
/// </summary>
[ByRefEvent]
public record struct GetVoiceRangeEvent(float Range);
