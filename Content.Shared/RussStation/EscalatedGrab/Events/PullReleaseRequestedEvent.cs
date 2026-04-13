namespace Content.Shared.RussStation.EscalatedGrab.Events;

/// <summary>
/// Raised on the puller when the release-pull keybind is pressed, so fork
/// systems can clear escalation state before the underlying pull stops.
/// </summary>
[ByRefEvent]
public record struct PullReleaseRequestedEvent(EntityUid Puller, EntityUid Pulled);
