using System.Linq;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Nutrition;
using Content.Shared.Nutrition.Components;
using Content.Shared.Popups;
using Content.Shared.RussStation.Skillchips.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared.RussStation.Skillchips.Consumers;

/// <summary>
/// Consumer for the <c>wine_tasting</c> capability (WINE / Wine.Is.Not.Equal chip).
/// SS13's TRAIT_WINE_TASTER overrides a wine reagent's taste description to reveal
/// its vintage. SS14 has no vintage data, so the faithful analog is: a holder
/// identifies exactly what they just drank, by reagent name, instead of the
/// generic flavour profile everyone else gets.
/// </summary>
public sealed class WineTastingSystem : EntitySystem
{
    public const string WineTastingTag = "wine_tasting";

    [Dependency] private readonly SharedSkillchipSystem _skillchip = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<EdibleComponent, IngestedEvent>(OnIngested);
    }

    private void OnIngested(Entity<EdibleComponent> ent, ref IngestedEvent args)
    {
        if (!_skillchip.HasCapability(args.Target, WineTastingTag))
            return;

        var names = new List<string>();
        foreach (var quantity in args.Split.Contents)
        {
            if (_proto.TryIndex<ReagentPrototype>(quantity.Reagent.Prototype, out var reagent))
                names.Add(reagent.LocalizedName);
        }

        if (names.Count == 0)
            return;

        _popup.PopupEntity(
            Loc.GetString("skillchip-wine-tasting-identify", ("drink", string.Join(", ", names.Distinct()))),
            args.Target,
            args.Target);
    }
}
