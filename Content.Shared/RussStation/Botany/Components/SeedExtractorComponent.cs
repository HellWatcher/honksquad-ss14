// HONK START
// This has been moved here from Content.Server/Botany/Components/SeedExtractorComponent.cs
// This is necessary as the seed extractor needs to be a shared component to enable clients
// to implement/predict certain behavior on the client-side. This avoids some graphical glitches.

using Content.Shared.Whitelist;
using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Botany.Components;

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

// HONK END
