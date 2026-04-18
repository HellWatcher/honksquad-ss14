using Content.Shared.FixedPoint;
using Robust.Shared.Serialization;

namespace Content.Shared.RussStation.MedicalScanner;

[Serializable, NetSerializable]
public sealed class HealthAnalyzerReagentState : BoundUserInterfaceState
{
    public NetEntity Target;
    public string TargetName;
    public List<HealthAnalyzerReagentGroup> Groups;
    public bool Active;

    /// <summary>
    /// One-shot tab hint applied by the BUI when the state arrives. Null on ongoing
    /// update ticks so the player can keep whichever tab they manually switched to.
    /// </summary>
    public HealthAnalyzerTab? PreferredTab;

    public HealthAnalyzerReagentState(NetEntity target, string targetName, List<HealthAnalyzerReagentGroup> groups, bool active = true, HealthAnalyzerTab? preferredTab = null)
    {
        Target = target;
        TargetName = targetName;
        Groups = groups;
        Active = active;
        PreferredTab = preferredTab;
    }
}

[Serializable, NetSerializable]
public struct HealthAnalyzerReagentGroup
{
    public string Label;
    public FixedPoint2 Volume;
    public FixedPoint2 MaxVolume;
    public List<HealthAnalyzerReagentEntry> Reagents;

    public HealthAnalyzerReagentGroup(string label, FixedPoint2 volume, FixedPoint2 maxVolume, List<HealthAnalyzerReagentEntry> reagents)
    {
        Label = label;
        Volume = volume;
        MaxVolume = maxVolume;
        Reagents = reagents;
    }
}

[Serializable, NetSerializable]
public struct HealthAnalyzerReagentEntry
{
    public string ReagentId;
    public string ReagentName;
    public Color Color;
    public FixedPoint2 Quantity;
    public bool Overdose;
    public bool Underdose;

    public HealthAnalyzerReagentEntry(string reagentId, string reagentName, Color color, FixedPoint2 quantity, bool overdose, bool underdose)
    {
        ReagentId = reagentId;
        ReagentName = reagentName;
        Color = color;
        Quantity = quantity;
        Overdose = overdose;
        Underdose = underdose;
    }
}
