using Content.Shared.Atmos;
using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Body;

/// <summary>
/// Marker component for advanced cybernetic lungs that filter toxic gases.
/// Filters all inhaled gases except those that oxygenate the host species.
/// The oxygenating gases are resolved from reagent prototypes on organ insertion.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CyberneticLungsComponent : Component
{
    /// <summary>
    /// Fraction of toxic gas moles filtered out before reaching the lungs.
    /// 0.5 = 50% reduction.
    /// </summary>
    [DataField]
    public float FilterFraction = 0.5f;

    /// <summary>
    /// Gases that oxygenate the host species; excluded from filtering.
    /// Populated at runtime when the organ is inserted into a body.
    /// </summary>
    [ViewVariables]
    public HashSet<Gas> OxygenatingGases = new();
}
