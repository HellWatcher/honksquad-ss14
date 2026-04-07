using Robust.Shared.Map;

namespace Content.Server.RussStation.Atmos;

/// <summary>
///     Raised as a broadcast event by gas reactions that should emit a radiation pulse.
///     Collected and batched by <see cref="Systems.AtmosRadiationPulseSystem"/>.
/// </summary>
[ByRefEvent]
public record struct AtmosRadiationPulseEvent(EntityUid GridUid, Vector2i Tile, float Rads);
