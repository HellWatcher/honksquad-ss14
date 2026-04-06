using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Body;

/// <summary>
/// Marker placed on a status effect entity to indicate breathing suppression.
/// When applied, adds <see cref="ActiveBreathingSuppressedComponent"/> to the target body,
/// preventing lungs from processing inhaled gas and causing natural suffocation.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BreathingSuppressedComponent : Component;

/// <summary>
/// Marker placed on a body entity whose breathing is currently suppressed.
/// Managed by <see cref="BreathingSuppressedComponent"/> status effect lifecycle.
/// </summary>
[RegisterComponent]
public sealed partial class ActiveBreathingSuppressedComponent : Component;
