using Content.Shared.FixedPoint;
using Robust.Shared.Serialization;

namespace Content.Shared.RussStation.Botany;

[Serializable, NetSerializable]
public sealed class PlantAnalyzerScannedUserMessage : BoundUserInterfaceState
{
    public PlantAnalyzerUiState State;

    public PlantAnalyzerScannedUserMessage(PlantAnalyzerUiState state)
    {
        State = state;
    }
}

[Serializable, NetSerializable]
public struct PlantAnalyzerUiState
{
    public string SeedName = "";

    // Basic stats
    public float Lifespan;
    public float Maturation;
    public float Production;
    public int Yield;
    public float Potency;
    public int GrowthStages;
    public string HarvestRepeat = "";
    public float Endurance;

    // Advanced stats
    public float IdealLight;
    public float WaterConsumption;
    public float NutrientConsumption;
    public float IdealHeat;
    public float HeatTolerance;
    public float LightTolerance;
    public float ToxinsTolerance;
    public float LowPressureTolerance;
    public float HighPressureTolerance;
    public float PestTolerance;
    public float WeedTolerance;

    // True when the analyzer is actively tracking the target; false when paused/stopped.
    public bool ScanMode;

    // Pre-localized collections — only present entries included
    public List<string> Traits = new();
    public Dictionary<string, FixedPoint2> Chemicals = new();
    public Dictionary<string, float> ConsumeGases = new();
    public Dictionary<string, float> ExudeGases = new();

    public PlantAnalyzerUiState() { }
}
