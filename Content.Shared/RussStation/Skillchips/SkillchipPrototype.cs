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
}
