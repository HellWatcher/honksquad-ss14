using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Examine;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.RussStation.Skillchips.Systems;

namespace Content.Shared.RussStation.Skillchips.Consumers;

/// <summary>
/// Examine hook for the INTJ skillchip (<c>flavour_calculus</c> capability).
/// When a chip holder examines anything edible inside details range, append
/// the same flavour line the food would print on a bite, plus the SS13
/// "needs more salt" nudge when the solution has no table salt. Mirrors the
/// SS13 TRAIT_REMOTE_TASTING hook in edible.dm. Subscribes on
/// <see cref="EdibleComponent"/> so every food and drink inherits the hook
/// without per-prototype marking. Shared so the client predicts the line.
/// </summary>
public sealed partial class SharedFlavourCalculusSystem : EntitySystem
{
    public const string FlavourCalculusTag = "flavour_calculus";
    private const string FoodSolutionName = "food";
    private const string DrinkSolutionName = "drink";
    private const string SaltReagentId = "TableSalt";

    [Dependency] private SharedSkillchipSystem _skillchip = default!;
    [Dependency] private SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private FlavorProfileSystem _flavorProfile = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<EdibleComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<EdibleComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (!_skillchip.HasCapability(args.Examiner, FlavourCalculusTag))
            return;

        // Try the edible's own named solution first, then fall back to the
        // two stock names so drink-shaped edibles still register.
        Entity<SolutionComponent>? solutionEntity = null;
        if (!_solutionContainer.ResolveSolution(ent.Owner, ent.Comp.Solution, ref solutionEntity, out var solution)
            && !_solutionContainer.ResolveSolution(ent.Owner, FoodSolutionName, ref solutionEntity, out solution)
            && !_solutionContainer.ResolveSolution(ent.Owner, DrinkSolutionName, ref solutionEntity, out solution))
        {
            return;
        }

        if (solution.Volume <= 0)
            return;

        var flavors = _flavorProfile.GetLocalizedFlavorsMessage(ent.Owner, args.Examiner, solution);
        args.PushMarkup(flavors);

        if (solution.GetTotalPrototypeQuantity(SaltReagentId) <= 0)
            args.PushMarkup(Loc.GetString("skillchip-flavour-calculus-needs-salt"));
    }
}
