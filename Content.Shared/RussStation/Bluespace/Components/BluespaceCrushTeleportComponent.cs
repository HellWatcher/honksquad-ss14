// HONK - Issue #302: bluespace crystal crush-and-throw teleport. Mirrors SS13's
// /obj/item/stack/ore/bluespace_crystal blink mechanic: using a crystal in-hand
// crushes one off the stack and short-range teleports the user; throwing one at
// a living target consumes it and teleports the target.
//
// Attached to MaterialBluespace, the only bluespace entity in the fork (no
// separate raw-ore form: tg ships the same sprite for ore and refined, so
// mining drops the crystal directly).

using Content.Shared.RussStation.Bluespace.EntitySystems;
using Robust.Shared.GameStates;
using BluespaceConstants = Content.Shared.RussStation.Bluespace.BluespaceConstants;

namespace Content.Shared.RussStation.Bluespace.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(BluespaceCrushTeleportSystem))]
public sealed partial class BluespaceCrushTeleportComponent : Component
{
    /// <summary>
    /// Maximum random offset (in tiles) the teleport throws the target. Mirrors
    /// SS13's <c>blink_range</c> on the natural raw crystal (8 tiles); the
    /// artificial / lab-grown variant from #302 follow-ups would use 4.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float BlinkRange = BluespaceConstants.DefaultBlinkRange;
}
