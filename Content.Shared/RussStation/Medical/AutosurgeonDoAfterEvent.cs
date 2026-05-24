using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.RussStation.Medical;

[Serializable, NetSerializable]
public sealed partial class AutosurgeonDoAfterEvent : SimpleDoAfterEvent;
