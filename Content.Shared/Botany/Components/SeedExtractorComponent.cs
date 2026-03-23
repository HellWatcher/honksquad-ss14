using Content.Shared.Whitelist;
using Robust.Shared.GameStates;

namespace Content.Shared.Botany.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class SeedExtractorComponent : Component
{
    /// <summary>
    /// The minimum amount of seed packets dropped when extracting from produce.
    /// </summary>
    [DataField("baseMinSeeds"), ViewVariables(VVAccess.ReadWrite)]
    public int BaseMinSeeds = 1;

    /// <summary>
    /// The maximum amount of seed packets dropped when extracting from produce.
    /// </summary>
    [DataField("baseMaxSeeds"), ViewVariables(VVAccess.ReadWrite)]
    public int BaseMaxSeeds = 3;

    /// <summary>
    /// The ID of the container used to store seed packets placed inside the extractor.
    /// </summary>
    [DataField]
    public string SeedContainerId = "seed_extractor_seeds";

    /// <summary>
    /// Whitelist controlling which items the extractor will accept.
    /// </summary>
    [DataField]
    public EntityWhitelist? Whitelist;
}
