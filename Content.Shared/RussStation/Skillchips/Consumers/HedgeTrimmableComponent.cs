using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.RussStation.Skillchips.Consumers;

/// <summary>
/// Marks an entity (potted plants) that a holder of the <c>hedgetrimming</c>
/// capability can reshape with a sharp item. SS13 parallel: TRAIT_BONSAI on
/// <c>/obj/item/kirbyplants</c>, which re-rolls the plant's visual on trim.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class HedgeTrimmableComponent : Component
{
    /// <summary>
    /// Sprite states this plant can be trimmed into. Defaults to the
    /// potted_plants.rsi plant variants.
    /// </summary>
    [DataField]
    public List<string> States = new()
    {
        "applebush",
        "plant-01", "plant-02", "plant-03", "plant-04", "plant-05",
        "plant-06", "plant-07", "plant-08", "plant-09", "plant-10",
        "plant-11", "plant-12", "plant-13", "plant-14", "plant-15",
        "plant-16", "plant-17", "plant-18", "plant-19", "plant-20",
        "plant-21", "plant-22", "plant-23", "plant-24", "plant-26",
        "plant-27", "plant-28", "plant-29", "plant-30",
    };

    /// <summary>
    /// Current trimmed state. Null until first trimmed; the client applies it
    /// to sprite layer 0 on state change.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string? CurrentState;

    /// <summary>How long a trim takes.</summary>
    [DataField]
    public TimeSpan TrimDuration = TimeSpan.FromSeconds(HedgeTrimmingConstants.TrimSeconds);
}

[Serializable, NetSerializable]
public sealed partial class HedgeTrimDoAfterEvent : SimpleDoAfterEvent;
