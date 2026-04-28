using Content.Shared.Body.Components;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Metabolism;

/// <summary>
///     Handles metabolizing various reagents with given effects.
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState, AutoGenerateComponentPause, Access(typeof(MetabolizerSystem))]
public sealed partial class MetabolizerComponent : Component
{
    /// <summary>
    ///     The next time that reagents will be metabolized.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextUpdate;

    /// <summary>
    ///     How often to metabolize reagents.
    /// </summary>
    /// <returns></returns>
    [DataField]
    public TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Multiplier applied to <see cref="UpdateInterval"/> for adjusting based on metabolic rate multiplier.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float UpdateIntervalMultiplier = 1f;

    /// <summary>
    /// Adjusted update interval based off of the multiplier value.
    /// </summary>
    [ViewVariables]
    public TimeSpan AdjustedUpdateInterval => UpdateInterval * UpdateIntervalMultiplier;

    /// <summary>
    ///     From which solution will this metabolizer attempt to metabolize chemicals for a given stage
    ///     This typically does not change and as such isn't networked.
    ///     TODO: Entity relations :(
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<MetabolismStagePrototype>, MetabolismSolutionEntry> Solutions = new()
    {
        ["Respiration"] = new()
        {
            SolutionName = "Lung",
            SolutionOnBody = false,
            TransferSolutionName = BloodstreamComponent.DefaultBloodSolutionName,
            MetabolizeAll = true
        },
        ["Digestion"] = new()
        {
            SolutionName = "stomach",
            SolutionOnBody = false,
            TransferSolutionName = BloodstreamComponent.DefaultBloodSolutionName,
            // HONK START - issue #679 step 4: align stomach throughput with SS13.
            //   * TransferEfficacy 1.0 (was 0.5): SS13 transfers stomach->blood 1:1, so
            //     a 30u oral dose actually arrives in blood as 30u, reachable for OD.
            //   * MinTransferPerTick mirrors SS13 STOMACH_METABOLISM_CONSTANT (0.25u).
            //   * VolumeScaledTransfer mirrors SS13 metabolism_efficiency (0.05) so a
            //     fuller stomach pushes faster.
            TransferEfficacy = 1,
            MinTransferPerTick = 0.25,
            VolumeScaledTransfer = 0.05f,
            // HONK END
        },
        ["Bloodstream"] = new()
        {
            SolutionName = BloodstreamComponent.DefaultBloodSolutionName,
            TransferSolutionName = BloodstreamComponent.DefaultMetabolitesSolutionName,
        },
        ["Metabolites"] = new()
        {
            SolutionName = BloodstreamComponent.DefaultMetabolitesSolutionName
        },
        // HONK START - issue #679 steps 1+6: Detoxification reads the bloodstream so the
        // liver can scrub toxin-class reagents in place. Step 6 wires the actual scrub rate
        // (LiverToxinScrubRate, see MetabolismConstants) so the liver pulls toxins out of
        // blood at a steady pace, mirroring SS13's liver filter.
        //
        // Engine quirk also handled here: TryMetabolizeStage's "reagent has no entry for the
        // stage" fallback removes TransferRate units into a transfer solution, or 1u flat if
        // no transfer solution. Self-loop the transfer to the same blood solution with
        // TransferRate = 0 so non-toxin reagents (Ethanol etc.) waiting for the heart's
        // Bloodstream transfer aren't quietly siphoned before they reach metabolites.
        ["Detoxification"] = new()
        {
            SolutionName = BloodstreamComponent.DefaultBloodSolutionName,
            TransferSolutionName = BloodstreamComponent.DefaultBloodSolutionName,
            TransferRate = 0,
            ToxinScrubRate = RussStation.Metabolism.MetabolismConstants.LiverToxinScrubRate,
        },
        // HONK END
        // HONK START - issue #679 step 2: Excretion reads the bloodstream so the kidney
        // can host the sub-tolerance filter (step 5) and the slow blood drain (step 7).
        // No reagent declares an Excretion entry yet, so the stage runs and does nothing
        // until later steps land. Same TransferRate = 0 self-loop as Detoxification so
        // the no-entry fallback can't drain Ethanol or other bloodstream reagents.
        ["Excretion"] = new()
        {
            SolutionName = BloodstreamComponent.DefaultBloodSolutionName,
            TransferSolutionName = BloodstreamComponent.DefaultBloodSolutionName,
            TransferRate = 0,
        }
        // HONK END
    };

    /// <summary>
    ///     Does this component use a solution on it's parent entity (the body) or itself
    /// </summary>
    /// <remarks>
    ///     Most things will use the parent entity (bloodstream).
    /// </remarks>
    [DataField]
    public bool SolutionOnBody = true;

    /// <summary>
    ///     List of metabolizer types that this organ is. ex. Human, Slime, Felinid, w/e.
    /// </summary>
    [DataField]
    [Access(typeof(MetabolizerSystem), Other = AccessPermissions.ReadExecute)] // FIXME Friends
    public HashSet<ProtoId<MetabolizerTypePrototype>>? MetabolizerTypes;

    /// <summary>
    ///     How many reagents can this metabolizer process at once?
    ///     Used to nerf 'stacked poisons' where having 5+ different poisons in a syringe, even at low
    ///     quantity, would be muuuuch better than just one poison acting.
    /// </summary>
    [DataField("maxReagents")]
    public int MaxReagentsProcessable = 3;

    /// <summary>
    ///     A list of metabolism stages that this metabolizer will act on, in order of precedence.
    /// </summary>
    [DataField]
    public List<ProtoId<MetabolismStagePrototype>> Stages = new();
}

[DataDefinition]
public sealed partial class MetabolismSolutionEntry
{
    /// <summary>
    /// From which solution will this metabolizer attempt to metabolize chemicals
    /// </summary>
    [DataField(required: true)]
    public string SolutionName;

    /// <summary>
    /// Does this metabolizer use a solution on it's parent entity (the body) or itself
    /// </summary>
    /// <remarks>
    /// Most things will use the parent entity (bloodstream).
    /// </remarks>
    [DataField]
    public bool SolutionOnBody = true;

    /// <summary>
    /// When true, this solution will be metabolized entirely instead of at a certain rate
    /// </summary>
    [DataField]
    public bool MetabolizeAll = false;

    /// <summary>
    /// Reagents without a metabolism for the current stage will be transferred to this solution
    /// </summary>
    [DataField]
    public string? TransferSolutionName;

    /// <summary>
    /// Reagents transferred by this metabolizer will transfer at this rate if they don't have a metabolism
    /// </summary>
    [DataField]
    public FixedPoint2 TransferRate = 0.25;

    /// <summary>
    /// The percentage of transferred reagents that actually make it to the next step in metabolism if they don't have explicit metabolites
    /// </summary>
    [DataField]
    public FixedPoint2 TransferEfficacy = 1;

    /// <summary>
    /// Does this metabolizer transfer to a solution on the body or on the entity itself
    /// </summary>
    [DataField]
    public bool TransferSolutionOnBody = true;
}
