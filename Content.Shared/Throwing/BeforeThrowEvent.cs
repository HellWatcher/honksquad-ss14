using System.Numerics;

namespace Content.Shared.Throwing;

[ByRefEvent]
public struct BeforeThrowEvent
{
    public BeforeThrowEvent(EntityUid itemUid, Vector2 direction, float throwSpeed,  EntityUid playerUid)
    {
        ItemUid = itemUid;
        Direction = direction;
        ThrowSpeed = throwSpeed;
        PlayerUid = playerUid;
    }

    public EntityUid ItemUid { get; set; }
    //HONK START - mutable for Weak Arm quirk
    public Vector2 Direction { get; set; }
    //HONK END
    public float ThrowSpeed { get; set;}
    public EntityUid PlayerUid { get; }

    public bool Cancelled = false;
}
