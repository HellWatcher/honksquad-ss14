using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.RussStation.Skillchips;

[Serializable, NetSerializable]
public enum SkillchipStationUiKey : byte
{
    Key,
}

// ── BUI state ──────────────────────────────────────────────────────────────

[Serializable, NetSerializable]
public sealed class SkillchipStationBoundUserInterfaceState : BoundUserInterfaceState
{
    /// <summary>Whether a DoAfter operation is currently running.</summary>
    public bool Working;

    /// <summary>Inserted chip in the input tray, or null if empty.</summary>
    public SkillchipStationChipInfo? InsertedChip;

    /// <summary>Chips currently implanted in the user's brain.</summary>
    public List<SkillchipStationChipInfo> ImplantedChips;

    /// <summary>Capacity currently in use.</summary>
    public int UsedCapacity;

    /// <summary>Maximum brain capacity.</summary>
    public int MaxCapacity;

    public SkillchipStationBoundUserInterfaceState(
        bool working,
        SkillchipStationChipInfo? insertedChip,
        List<SkillchipStationChipInfo> implantedChips,
        int usedCapacity,
        int maxCapacity)
    {
        Working = working;
        InsertedChip = insertedChip;
        ImplantedChips = implantedChips;
        UsedCapacity = usedCapacity;
        MaxCapacity = maxCapacity;
    }
}

[Serializable, NetSerializable]
public sealed class SkillchipStationChipInfo
{
    public ProtoId<SkillchipPrototype> ChipProto;
    public string Name = string.Empty;
    public string Description = string.Empty;
    public int CapacityCost;
}

// ── BUI messages ───────────────────────────────────────────────────────────

/// <summary>Player requests to implant the chip currently in the tray.</summary>
[Serializable, NetSerializable]
public sealed class SkillchipStationImplantMessage : BoundUserInterfaceMessage;

/// <summary>Player requests to eject the chip from the tray without implanting.</summary>
[Serializable, NetSerializable]
public sealed class SkillchipStationEjectMessage : BoundUserInterfaceMessage;

/// <summary>Player requests to remove an implanted chip.</summary>
[Serializable, NetSerializable]
public sealed class SkillchipStationRemoveMessage(ProtoId<SkillchipPrototype> chipProto) : BoundUserInterfaceMessage
{
    public ProtoId<SkillchipPrototype> ChipProto = chipProto;
}

// ── DoAfter events ─────────────────────────────────────────────────────────

[Serializable, NetSerializable]
public sealed partial class SkillchipImplantDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class SkillchipRemoveDoAfterEvent : SimpleDoAfterEvent
{
    public ProtoId<SkillchipPrototype> ChipProto;

    public SkillchipRemoveDoAfterEvent(ProtoId<SkillchipPrototype> chipProto)
    {
        ChipProto = chipProto;
    }
}
