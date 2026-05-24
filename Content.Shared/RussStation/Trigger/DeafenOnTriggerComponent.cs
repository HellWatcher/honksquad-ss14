using Content.Shared.Trigger.Components.Effects;
using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Trigger;

/// <summary>
/// Applies temporary deafness to entities in an area when triggered. Mirrors
/// <see cref="FlashOnTriggerComponent"/> but for the audio side -- pair the two on a
/// flashbang and the grenade blinds and deafens everyone in range.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DeafenOnTriggerComponent : BaseXOnTriggerComponent
{
    /// <summary>
    /// The radius (in tiles) around the trigger source to deafen.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Range = DeafenOnTriggerConstants.DefaultRange;

    /// <summary>
    /// Duration of the temporary deafness applied to each affected entity.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan Duration = DeafenOnTriggerConstants.DefaultDuration;

    /// <summary>
    /// Per-entity probability of being deafened. Rolled separately for each target.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Probability = DeafenOnTriggerConstants.DefaultProbability;
}
