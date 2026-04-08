using Robust.Shared.Serialization;

namespace Content.Shared.RussStation.Wounds;

[Serializable, NetSerializable]
public enum WoundCategory : byte
{
    Fracture,
    Burn,
}
