using Content.Shared.RussStation.Surgery.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Surgery.Components;

/// <summary>
/// Marks an entity as draped with a bedsheet and ready for surgery.
/// The bedsheet entity is stored and dropped when surgery ends.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedSurgerySystem))]
public sealed partial class SurgeryDrapedComponent : Component
{
    /// <summary>
    /// The bedsheet or drape entity that was used to drape this patient.
    /// </summary>
    [AutoNetworkedField, DataField]
    public EntityUid? Bedsheet;

    /// <summary>
    /// Speed modifier from the draping material. Multiplies all surgery step durations.
    /// 1.0 for surgical drapes (standard), 1.5 for bedsheets (improvised penalty).
    /// </summary>
    [AutoNetworkedField, DataField]
    public float DrapeSpeedModifier = 1.5f;
}
