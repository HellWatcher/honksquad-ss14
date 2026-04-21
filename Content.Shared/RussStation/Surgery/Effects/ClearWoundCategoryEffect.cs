using Content.Shared.RussStation.Wounds;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared.RussStation.Surgery.Effects;

/// <summary>
/// Surgery effect: clears every wound in <see cref="Category"/> on the patient
/// when the step completes. Used by external repair procedures (fracture
/// setting, burn treatment) that don't need to open the body cavity and
/// should bypass the tend-wounds damage-healing path.
/// </summary>
[DataDefinition]
public sealed partial class ClearWoundCategoryEffect : ISurgeryEffect
{
    [DataField(required: true)]
    public WoundCategory Category;
}
