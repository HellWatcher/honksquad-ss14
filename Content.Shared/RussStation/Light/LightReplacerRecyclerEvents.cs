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

/// <summary>
///     Sent from client to server when the player wants to take one stored bulb of a given
///     prototype out of the replacer. The server pops it into the user's hands if possible,
///     or drops it at their feet otherwise.
/// </summary>
[Serializable, NetSerializable]
public sealed class LightReplacerExtractMessage(EntProtoId prototypeId) : BoundUserInterfaceMessage
{
    public EntProtoId PrototypeId = prototypeId;
}

[Serializable, NetSerializable]
public enum LightReplacerRecyclerUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class LightReplacerRecyclerBoundUserInterfaceState : BoundUserInterfaceState
{
    public int Points;
    public int PrintCost;
    public int PointsPerRecycle;
    public List<LightReplacerStoredBulb> Stored;
    public List<EntProtoId> Printable;

    public LightReplacerRecyclerBoundUserInterfaceState(
        int points,
        int printCost,
        int pointsPerRecycle,
        List<LightReplacerStoredBulb> stored,
        List<EntProtoId> printable)
    {
        Points = points;
        PrintCost = printCost;
        PointsPerRecycle = pointsPerRecycle;
        Stored = stored;
        Printable = printable;
    }
}

[Serializable, NetSerializable]
public record struct LightReplacerStoredBulb(EntProtoId ProtoId, int Count);

/// <summary>
///     Raised on the light replacer after a broken bulb is ejected during replacement.
///     Allows the recycler system to intercept and consume it.
/// </summary>
public sealed class LightReplacerBulbReplacedEvent(EntityUid brokenBulb, EntityUid user) : EntityEventArgs
{
    public EntityUid BrokenBulb = brokenBulb;
    public EntityUid User = user;
}

/// <summary>
///     Raised on the light replacer when the player manually tries to insert a non-Normal bulb
///     (broken or burned) into it. The recycler subscribes and consumes the bulb for points
///     instead of letting the upstream reject fire. Set <see cref="Handled"/> to true to signal
///     that the attempt was accepted; false leaves the reject path intact.
/// </summary>
[ByRefEvent]
public struct LightReplacerBrokenBulbInsertEvent
{
    public EntityUid BulbUid;
    public EntityUid? UserUid;
    public bool Handled;

    public LightReplacerBrokenBulbInsertEvent(EntityUid bulbUid, EntityUid? userUid)
    {
        BulbUid = bulbUid;
        UserUid = userUid;
    }
}

/// <summary>
///     Raised on the light replacer when a fixture replacement is requested. The recycler takes
///     ownership of the whole replace flow: eat the broken bulb for points, then pick a replacement
///     from storage (exact-match first, same-type fallback) or print one from accumulated points.
///     Set <see cref="Handled"/> to skip upstream's default replace logic.
/// </summary>
[ByRefEvent]
public struct LightReplacerRecycleReplaceEvent
{
    public EntityUid FixtureUid;
    public EntityUid? UserUid;
    public EntityUid? FixtureBulbUid;
    public bool Handled;
    public bool Success;

    public LightReplacerRecycleReplaceEvent(EntityUid fixtureUid, EntityUid? userUid, EntityUid? fixtureBulbUid)
    {
        FixtureUid = fixtureUid;
        UserUid = userUid;
        FixtureBulbUid = fixtureBulbUid;
    }
}
