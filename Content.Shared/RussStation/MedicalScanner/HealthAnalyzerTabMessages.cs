using Robust.Shared.Serialization;

namespace Content.Shared.RussStation.MedicalScanner;

/// <summary>
/// Which tab the tabbed health analyzer UI should select.
/// Mirrors the tab index in <c>HealthAnalyzerTabbedWindow.xaml</c>.
/// </summary>
[Serializable, NetSerializable]
public enum HealthAnalyzerTab : byte
{
    Health = 0,
    Reagents = 1,
    Wounds = 2,
}
