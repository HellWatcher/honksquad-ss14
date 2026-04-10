using Content.Shared.Medical;
using Content.Shared.Nutrition;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Shared.RussStation.Traits;

public sealed class VegetarianSystem : EntitySystem
{
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly VomitSystem _vomit = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private static readonly ProtoId<TagPrototype> MeatTag = "Meat";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VegetarianComponent, IngestingEvent>(OnIngesting);
    }

    private void OnIngesting(EntityUid uid, VegetarianComponent component, ref IngestingEvent args)
    {
        if (!_tag.HasTag(args.Food, MeatTag))
            return;

        _popup.PopupPredicted(Loc.GetString("trait-vegetarian-nausea"), uid, uid, PopupType.MediumCaution);
        _vomit.Vomit(uid, -20f, -20f);
    }
}
