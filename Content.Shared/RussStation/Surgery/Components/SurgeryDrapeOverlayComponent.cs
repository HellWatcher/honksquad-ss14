using Robust.Shared.Prototypes;

namespace Content.Shared.RussStation.Surgery.Components;

/// <summary>
/// Attached to a draping item (bedsheet, surgical drape) to specify which overlay entity gets
/// spawned above the patient when the item is used to drape them for surgery. The overlay entity
/// is parented to the patient so it follows them until surgery ends and the draped component is
/// removed.
/// </summary>
[RegisterComponent]
public sealed partial class SurgeryDrapeOverlayComponent : Component
{
    /// <summary>
    /// Entity prototype spawned on the patient as a visible overlay while they're draped.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId OverlayPrototype;
}
