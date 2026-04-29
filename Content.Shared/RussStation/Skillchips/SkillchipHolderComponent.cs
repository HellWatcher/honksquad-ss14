using Content.Shared.RussStation.Skillchips.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.RussStation.Skillchips;

/// <summary>
/// Placed on brain entities. Tracks which skillchips are implanted and which capability
/// tags are currently active. Lives on the brain so it travels with body transfers and cloning.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedSkillchipSystem))]
public sealed partial class SkillchipHolderComponent : Component
{
    /// <summary>
    /// Chips currently implanted in this brain.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<ProtoId<SkillchipPrototype>> ImplantedChips = new();

    /// <summary>
    /// Action entity UIDs spawned by chip grants, keyed by chip proto + action proto.
    /// Populated at runtime; not serialized.
    /// </summary>
    [DataField]
    public Dictionary<string, EntityUid> ActionEntities = new();

    /// <summary>
    /// Capability tags currently active on this brain.
    /// Systems query these via SharedSkillchipSystem.HasCapability.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<string> CapabilityTags = new();

    /// <summary>
    /// Maximum total capacity cost of implanted chips.
    /// </summary>
    [DataField]
    public int MaxCapacity = SkillchipsConstants.DefaultMaxCapacity;
}
