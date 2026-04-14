using Robust.Shared.Serialization;

namespace Content.Shared.MedicalScanner;

[Serializable, NetSerializable]
public enum HealthAnalyzerUiKey : byte
{
    Key,
    //HONK START - Separate UI for the alt-verb reagent scanner.
    Reagents,
    //HONK END
}
