// HONK - Issue #697 parent: shared bluespace plant teleport trait. Mirrors SS13's
// /datum/plant_gene/trait/teleport ("Bluespace Activity"). Attached to a fork plant
// entity (food, peel) to teleport the target on slip and on throw-impact.

using Content.Shared.RussStation.Bluespace.EntitySystems;
using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Bluespace.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(BluespacePlantTeleportSystem))]
public sealed partial class BluespacePlantTeleportComponent : Component
{
    /// <summary>
    /// Maximum random teleport offset (in tiles). SS13's
    /// <c>/datum/plant_gene/trait/teleport</c> scales by potency / 10; this is the
    /// flat per-entity equivalent for the fork.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float BlinkRange = BluespaceConstants.PlantBlinkRange;

    /// <summary>
    /// Fire on slip events (someone slipping on this entity). Mirrors the
    /// <c>slip_teleport</c> branch of the SS13 trait, used when the parent plant
    /// has the slip gene without squash (banana peel).
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool OnSlip = true;

    /// <summary>
    /// Fire on throw-impact (this entity hits a target). Mirrors the
    /// <c>squash_teleport</c> branch of the SS13 trait, used when the parent plant
    /// has the squash gene (bluespace tomato).
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool OnThrowImpact = true;
}
