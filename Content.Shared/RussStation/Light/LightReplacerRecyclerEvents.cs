using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.RussStation.Light;

/// <summary>
///     Sent from client to server when the player selects a bulb type to print.
/// </summary>
[Serializable, NetSerializable]
public sealed class LightReplacerPrintMessage(EntProtoId prototypeId) : BoundUserInterfaceMessage
{
    public EntProtoId PrototypeId = prototypeId;
}

[Serializable, NetSerializable]
public enum LightReplacerRecyclerUiKey : byte
{
    Key,
}

/// <summary>
///     Raised on the light replacer after a broken bulb is ejected during replacement.
///     Allows the recycler system to intercept and consume it.
/// </summary>
public sealed class LightReplacerBulbReplacedEvent(EntityUid brokenBulb, EntityUid user) : EntityEventArgs
{
    public EntityUid BrokenBulb = brokenBulb;
    public EntityUid User = user;
}
