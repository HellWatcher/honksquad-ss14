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
using Robust.Shared.Prototypes;
using BluespaceConstants = Content.Shared.RussStation.Bluespace.BluespaceConstants;

namespace Content.Shared.RussStation.Bluespace.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(BluespaceBlinkSystem))]
public sealed partial class BluespaceCrushTeleportComponent : Component
{
    /// <summary>
    /// Maximum random offset (in tiles) the teleport throws the target. Mirrors
    /// SS13's <c>blink_range</c> on the natural raw crystal (8 tiles); the
    /// artificial / lab-grown variant from #302 follow-ups would use 4.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float BlinkRange = BluespaceConstants.DefaultBlinkRange;

    /// <summary>
    /// Visual effect spawned at the source tile when a blink fires. Defaults
    /// to the SS13 phaseout animation. Set to null to suppress.
    /// </summary>
    [DataField]
    public EntProtoId? BlinkEffectSource = "EffectPhaseOut";

    /// <summary>
    /// Visual effect spawned at the destination tile. Defaults to the SS13
    /// phasein animation, the canonical companion to do_teleport's
    /// phasein.ogg arrival cue.
    /// </summary>
    [DataField]
    public EntProtoId? BlinkEffectDestination = "EffectPhaseIn";
}
