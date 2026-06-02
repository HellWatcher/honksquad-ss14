namespace Content.Server.RussStation.Botany.Components;

/// <summary>
///     Marker added to plant holders at map init when <c>botany.overhaul_enabled</c> is true.
///     This is the proof-of-concept hook point for the botany overhaul: it lets overhaul
///     behavior attach to trays without editing any upstream botany system, so the toggle
///     can be flipped per-server with zero merge surface against upstream.
/// </summary>
[RegisterComponent]
public sealed partial class BotanyOverhaulComponent : Component
{
}
