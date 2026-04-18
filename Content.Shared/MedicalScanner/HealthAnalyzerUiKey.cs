using Robust.Shared.Serialization;

namespace Content.Shared.MedicalScanner;

[Serializable, NetSerializable]
public enum HealthAnalyzerUiKey : byte
{
    Key
    //HONK START - Alt-verb reagent scanner UI
    , Reagents
    //HONK END
}
