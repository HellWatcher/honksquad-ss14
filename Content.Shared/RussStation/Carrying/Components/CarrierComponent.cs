using Content.Shared.RussStation.Carrying.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Carrying.Components;

/// <summary>
/// Indicates this entity can fireman carry another entity, and configures
/// the speed penalties and visual offset while doing so.
/// The active relationship lives on <see cref="ActiveCarrierComponent"/>;
/// this component is config-only.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedCarryingSystem))]
public sealed partial class CarrierComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float WalkSpeedModifier = 0.75f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float SprintSpeedModifier = 0.6f;

    /// <summary>
    /// Y offset for the carried entity visual. Overridable per prototype for different species heights.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float CarryOffset = 0.2f;
}
