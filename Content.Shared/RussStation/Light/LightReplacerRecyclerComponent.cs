using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared.RussStation.Light;

/// <summary>
///     Allows a light replacer to recycle broken bulbs into new ones.
///     Broken bulbs collected during replacement add recycle points.
///     Players can spend points to print new bulbs via a radial menu.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LightReplacerRecyclerComponent : Component
{
    /// <summary>
    ///     Current accumulated recycle points.
    /// </summary>
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public int RecyclePoints;

    /// <summary>
    ///     Points gained per broken bulb recycled.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int PointsPerRecycle = LightReplacerRecyclerConstants.DefaultPointsPerRecycle;

    /// <summary>
    ///     Points required to print one new bulb.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int PrintCost = LightReplacerRecyclerConstants.DefaultPrintCost;

    /// <summary>
    ///     Cap on how many bulbs can live in the replacer's storage at once. Manual inserts, print
    ///     fallbacks, and the print-to-storage path all respect this. Default matches a fully
    ///     stocked light box (BoxLightbulb / BoxLighttube fill with 12 bulbs).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int MaxStoredBulbs = LightReplacerRecyclerConstants.DefaultMaxStoredBulbs;

    /// <summary>
    ///     Sound played when a broken bulb or glass shard is consumed for recycle points.
    /// </summary>
    [DataField]
    public SoundSpecifier RecycleSound = new SoundPathSpecifier("/Audio/@RussStation/LightReplacer/russstation_sound_machines_click.ogg");

    /// <summary>
    ///     Sound played when a new bulb is successfully printed from recycle points. Pulled from
    ///     the SS13 light replacer's "fabricated bulb from shards" cue.
    /// </summary>
    [DataField]
    public SoundSpecifier PrintSound = new SoundPathSpecifier("/Audio/@RussStation/LightReplacer/russstation_sound_machines_ding.ogg");

    /// <summary>
    ///     Entity prototypes that can be printed from the radial menu.
    /// </summary>
    [DataField]
    public List<EntProtoId> PrintablePrototypes = new()
    {
        "LightBulb",
        "LedLightBulb",
        "DimLightBulb",
        "WarmLightBulb",
        "ServiceLightBulb",
        "LightTube",
        "LedLightTube",
        "ExteriorLightTube",
        "SodiumLightTube",
    };
}
