namespace Content.Shared.RussStation.EscalatedGrab.Events;

/// <summary>
/// Raised on the puller after a grab stage change, for other systems to react.
/// </summary>
[ByRefEvent]
public record struct GrabEscalatedEvent(EntityUid Puller, EntityUid Target, GrabStage OldStage, GrabStage NewStage);
