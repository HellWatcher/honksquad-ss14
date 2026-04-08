namespace Content.Shared.RussStation.EscalatedGrab.Events;

/// <summary>
/// Raised on the target when they start resisting a grab.
/// Subscribers (e.g. Pushover quirk) can modify <see cref="ResistTime"/> to make resisting harder/easier.
/// </summary>
[ByRefEvent]
public record struct GrabResistAttemptEvent(EntityUid Puller, EntityUid Pulled, GrabStage Stage, TimeSpan ResistTime);
