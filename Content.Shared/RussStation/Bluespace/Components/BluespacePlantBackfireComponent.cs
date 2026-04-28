// HONK - Issue #697 parent: bluespace tomato hold-backfire trait. Mirrors SS13's
// /datum/plant_gene/trait/backfire/bluespace ("Bluespace Volatility"). On UseInHand
// the entity rolls a probability and, on success, drops itself, fires a teleport on
// the user, and is consumed (mimicking the squash_plant call SS13 makes). The user
// is the target both because they're holding it unprotected and because the
// fumble lands the squash on them, not on a tile.

using Content.Shared.RussStation.Bluespace.EntitySystems;
using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Bluespace.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(BluespacePlantTeleportSystem))]
public sealed partial class BluespacePlantBackfireComponent : Component
{
    /// <summary>
    /// Probability per UseInHand that the fumble fires.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Probability = BluespaceConstants.PlantBackfireProbability;

    /// <summary>
    /// Teleport range applied to the user when the fumble fires. Defaults to the
    /// same range as <see cref="BluespacePlantTeleportComponent.BlinkRange"/>; can
    /// be tuned independently if backfire ought to feel weaker than the
    /// regular squash.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float BlinkRange = BluespaceConstants.PlantBlinkRange;
}
