using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.RussStation.MedicalScanner;

[Serializable, NetSerializable]
public sealed partial class HealthAnalyzerReagentDoAfterEvent : SimpleDoAfterEvent
{
}
