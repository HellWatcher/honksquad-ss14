using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Body;

/// <summary>
/// Marker placed on a status effect entity to indicate cardiac arrest.
/// When applied, adds <see cref="ActiveCardiacArrestComponent"/> to the target body,
/// which accelerates oxygen saturation loss (heart not pumping blood).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CardiacArrestComponent : Component;

/// <summary>
/// Marker placed on a body entity currently in cardiac arrest.
/// Managed by <see cref="CardiacArrestComponent"/> status effect lifecycle.
/// </summary>
[RegisterComponent]
public sealed partial class ActiveCardiacArrestComponent : Component;
