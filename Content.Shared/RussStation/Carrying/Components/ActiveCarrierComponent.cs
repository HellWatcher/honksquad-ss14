using Content.Shared.RussStation.Carrying.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Carrying.Components;

/// <summary>
/// Active marker on an entity currently carrying another entity.
/// Owns the carrier-to-target reference: the marker IS the relationship,
/// so it cannot drift out of sync with anything else. <see cref="BeingCarriedComponent"/>
/// is the symmetric marker on the target side.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedCarryingSystem))]
public sealed partial class ActiveCarrierComponent : Component
{
    /// <summary>
    /// The entity being carried. Set atomically with the marker's add and
    /// guaranteed valid for the marker's entire lifetime.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid Target;
}
