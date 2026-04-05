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
    public int PointsPerRecycle = 1;

    /// <summary>
    ///     Points required to print one new bulb.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int PrintCost = 3;

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
