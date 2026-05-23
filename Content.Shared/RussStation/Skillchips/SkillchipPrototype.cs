using Robust.Shared.Prototypes;

namespace Content.Shared.RussStation.Skillchips;

[Prototype]
public sealed partial class SkillchipPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public string Name = string.Empty;

    [DataField]
    public string Description = string.Empty;

    /// <summary>
    /// How many brain capacity slots this chip occupies.
    /// </summary>
    [DataField]
    public int CapacityCost = SkillchipsConstants.DefaultCapacityCost;

    /// <summary>
    /// Optional category string. Only one chip of a given category may be implanted at a time.
    /// Null means no category restriction.
    /// </summary>
    [DataField]
    public string? Category;

    /// <summary>
    /// Grants applied to the mob when this chip is active.
    /// </summary>
    [DataField]
    public List<SkillchipGrant> Grants = new();
}
