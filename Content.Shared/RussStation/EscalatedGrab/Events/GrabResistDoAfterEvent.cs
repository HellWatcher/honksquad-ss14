using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.RussStation.EscalatedGrab.Events;

/// <summary>
/// DoAfter event for grab resist completion.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class GrabResistDoAfterEvent : SimpleDoAfterEvent;
