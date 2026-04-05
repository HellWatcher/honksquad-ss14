using Robust.Shared.Prototypes;

namespace Content.Shared.RussStation.CartridgeLoader;

[Prototype]
public sealed partial class ForkCartridgeSetPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public List<string> Cartridges { get; private set; } = new();

    /// <summary>
    /// Controls installation order across sets. Lower values install first.
    /// </summary>
    [DataField]
    public int Order { get; private set; }

    /// <summary>
    /// Optional list of component names the entity must have for this set to apply.
    /// If null or empty, applies to all entities with CartridgeLoader.
    /// </summary>
    [DataField]
    public List<string>? RequireComponents { get; private set; }

    /// <summary>
    /// Optional list of component names that exclude an entity from this set.
    /// If the entity has any of these components, the set is skipped.
    /// </summary>
    [DataField]
    public List<string>? ExcludeComponents { get; private set; }
}
