using Robust.Shared.GameObjects;

namespace Content.Shared.RussStation.Body;

/// <summary>
/// Marker on the status effect entity for EMP-induced temporary blindness.
/// The system bridges this to upstream blindness by adding/removing
/// <see cref="Content.Shared.Eye.Blinding.Components.BlindnessStatusEffectComponent"/>
/// on the same effect entity.
/// </summary>
[RegisterComponent]
public sealed partial class EmpBlindnessComponent : Component;
