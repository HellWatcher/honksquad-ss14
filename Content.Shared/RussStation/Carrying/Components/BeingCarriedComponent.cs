using Content.Shared.RussStation.Carrying.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Carrying.Components;

/// <summary>
/// Active marker on an entity currently being carried.
/// Owns the target-to-carrier reference: the marker IS the relationship,
/// so it cannot drift out of sync with anything else. <see cref="ActiveCarrierComponent"/>
/// is the symmetric marker on the carrier side.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedCarryingSystem))]
public sealed partial class BeingCarriedComponent : Component
{
    /// <summary>
    /// The entity carrying this one. Set atomically with the marker's add and
    /// guaranteed valid for the marker's entire lifetime.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid Carrier;

    /// <summary>
    /// Saved by the client CarryingSystem on startup so draw depth can be restored when carrying ends.
    /// </summary>
    [ViewVariables]
    public int? OriginalDrawDepth;
}
