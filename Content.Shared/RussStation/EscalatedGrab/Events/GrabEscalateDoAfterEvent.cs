using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.RussStation.EscalatedGrab.Events;

/// <summary>
/// DoAfter event for grab escalation completion.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class GrabEscalateDoAfterEvent : SimpleDoAfterEvent;
