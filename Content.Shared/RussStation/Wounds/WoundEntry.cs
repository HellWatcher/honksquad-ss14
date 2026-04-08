using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.RussStation.Wounds;

[DataDefinition, Serializable, NetSerializable]
public sealed partial class WoundEntry
{
    [DataField]
    public ProtoId<WoundTypePrototype> WoundTypeId;

    [DataField]
    public int Tier;

    [DataField]
    public TimeSpan TimeAtCurrentTier;

    public WoundEntry(ProtoId<WoundTypePrototype> woundTypeId, int tier, TimeSpan timeAtCurrentTier)
    {
        WoundTypeId = woundTypeId;
        Tier = tier;
        TimeAtCurrentTier = timeAtCurrentTier;
    }
}
