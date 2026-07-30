using Content.Shared.Body;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.RussStation.Nutrition;
using Content.Shared.RussStation.Skillchips.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared.RussStation.Skillchips.Consumers;

/// <summary>
/// Ingestion hook for the DET.ekt skillchip (<c>detective_taste</c> capability).
/// When a chip holder eats or drinks, replace the upstream taste line with a
/// reagent-name listing. Mirrors SS13's <c>TRAIT_DETECTIVES_TASTE</c> override
/// in <c>reagents.dm</c>: <c>get_taste_description</c> returns the reagent's
/// <c>name</c> when the taster has the trait.
///
/// Subscribes to <see cref="IngestionFlavorRewriteEvent"/>, a fork-side hook
/// IngestionSystem raises just before formatting the eat / force-feed popup.
/// Avoids the engine's ordered-event trap (unordered handlers fire before
/// ordered ones, so a <c>before:[IngestionSystem]</c> directed sub on a
/// different food-side component does not actually run first), and keeps
/// IngestionSystem free of fork-consumer dependencies.
///
/// TODO: SS13 also overrides this for blood, returning the blood type. SS14 has
/// no blood-type metadata on the reagent yet, so the blood branch is deferred.
/// </summary>
public sealed partial class SharedDetectiveTasteSystem : EntitySystem
{
    public const string DetectiveTasteTag = "detective_taste";

    [Dependency] private SharedSkillchipSystem _skillchip = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BodyComponent, IngestionFlavorRewriteEvent>(OnRewriteFlavor);
    }

    private void OnRewriteFlavor(Entity<BodyComponent> ent, ref IngestionFlavorRewriteEvent args)
    {
        if (args.Solution.Volume <= 0)
            return;

        if (!_skillchip.HasCapability(ent.Owner, DetectiveTasteTag))
            return;

        var names = new List<string>();
        foreach (var reagent in args.Solution.Contents)
        {
            if (_proto.TryIndex<ReagentPrototype>(reagent.Reagent.Prototype, out var proto))
                names.Add(proto.LocalizedName);
        }

        if (names.Count == 0)
            return;

        args.Flavors = Loc.GetString("skillchip-detective-taste-line", ("reagents", string.Join(", ", names)));
    }
}
