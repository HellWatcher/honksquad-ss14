using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Hearing;

/// <summary>
/// Marker placed on a body while it has the TemporaryDeafness status effect active.
/// Cancels CanHearAttemptEvent. Mirrors the
/// <see cref="Content.Shared.RussStation.Body.ActiveBreathingSuppressedComponent"/> pattern.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ActiveTemporaryDeafnessComponent : Component;
