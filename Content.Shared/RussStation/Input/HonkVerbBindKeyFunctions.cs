using Robust.Shared.Input;

namespace Content.Shared.RussStation.Input;

// HONK 20 slot-style key functions for the verb bindings system (#579).
// Each slot can be assigned to an emote (and later, curated gameplay verbs)
// via the options UI. The slots live here rather than in ContentKeyFunctions
// so they stay out of upstream and won't conflict during rebase.
[KeyFunctions]
public static class HonkVerbBindKeyFunctions
{
    public const int SlotCount = 20;

    public static readonly BoundKeyFunction Slot0  = "HonkVerbBind0";
    public static readonly BoundKeyFunction Slot1  = "HonkVerbBind1";
    public static readonly BoundKeyFunction Slot2  = "HonkVerbBind2";
    public static readonly BoundKeyFunction Slot3  = "HonkVerbBind3";
    public static readonly BoundKeyFunction Slot4  = "HonkVerbBind4";
    public static readonly BoundKeyFunction Slot5  = "HonkVerbBind5";
    public static readonly BoundKeyFunction Slot6  = "HonkVerbBind6";
    public static readonly BoundKeyFunction Slot7  = "HonkVerbBind7";
    public static readonly BoundKeyFunction Slot8  = "HonkVerbBind8";
    public static readonly BoundKeyFunction Slot9  = "HonkVerbBind9";
    public static readonly BoundKeyFunction Slot10 = "HonkVerbBind10";
    public static readonly BoundKeyFunction Slot11 = "HonkVerbBind11";
    public static readonly BoundKeyFunction Slot12 = "HonkVerbBind12";
    public static readonly BoundKeyFunction Slot13 = "HonkVerbBind13";
    public static readonly BoundKeyFunction Slot14 = "HonkVerbBind14";
    public static readonly BoundKeyFunction Slot15 = "HonkVerbBind15";
    public static readonly BoundKeyFunction Slot16 = "HonkVerbBind16";
    public static readonly BoundKeyFunction Slot17 = "HonkVerbBind17";
    public static readonly BoundKeyFunction Slot18 = "HonkVerbBind18";
    public static readonly BoundKeyFunction Slot19 = "HonkVerbBind19";

    public static BoundKeyFunction[] All { get; } =
    {
        Slot0, Slot1, Slot2, Slot3, Slot4, Slot5, Slot6, Slot7, Slot8, Slot9,
        Slot10, Slot11, Slot12, Slot13, Slot14, Slot15, Slot16, Slot17, Slot18, Slot19,
    };
}
