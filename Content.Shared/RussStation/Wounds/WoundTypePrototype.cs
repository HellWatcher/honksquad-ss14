using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared.RussStation.Wounds;

/// <summary>
/// Defines a wound type with damage thresholds, tier names, and degradation timing.
/// </summary>
[Prototype]
public sealed partial class WoundTypePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public WoundCategory Category;

    /// <summary>
    /// The damage type that triggers this wound (e.g. Blunt, Heat, Cold).
    /// </summary>
    [DataField(required: true)]
    public string DamageType = string.Empty;

    /// <summary>
    /// Damage spike thresholds for tier 1, 2, and 3.
    /// </summary>
    [DataField(required: true)]
    public float[] Thresholds = new float[3];

    /// <summary>
    /// Seconds before a wound degrades one tier. 0 means no degradation.
    /// </summary>
    [DataField]
    public float DegradationTime;

    /// <summary>
    /// Display names per tier (1-indexed).
    /// </summary>
    [DataField(required: true)]
    public Dictionary<int, string> Names = new();
}
