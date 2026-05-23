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
    /// Grants applied to the mob when this chip is active.
    /// </summary>
    [DataField]
    public List<SkillchipGrant> Grants = new();

    /// <summary>
    /// Optional mutual-exclusion key. A brain can hold at most one chip per non-null
    /// category, so SS13-style job chips (chef, janitor, etc.) sharing a category
    /// reject install attempts past the first. Null means no restriction.
    /// </summary>
    [DataField]
    public string? Category;
}
