using System.Numerics;
using Content.Shared.Medical;
using Content.Shared.Nutrition;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Shared.RussStation.Traits;

public sealed partial class VegetarianSystem : EntitySystem
{
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private VomitSystem _vomit = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

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

        var coords = _transform.GetMoverCoordinates(uid).Offset(new Vector2(TraitsConstants.Vegetarian.PopupXOffset, TraitsConstants.Vegetarian.PopupYOffset));
        _popup.PopupPredictedCoordinates(Loc.GetString("trait-vegetarian-nausea"), coords, uid, PopupType.MediumCaution);
        _vomit.Vomit(uid, TraitsConstants.Vegetarian.VomitThirstAmount, TraitsConstants.Vegetarian.VomitHungerAmount);
    }
}
