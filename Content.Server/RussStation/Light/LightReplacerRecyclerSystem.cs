using Content.Server.Light.Components;
using Content.Shared.Examine;
using Content.Shared.Light.Components;
using Content.Shared.Popups;
using Content.Shared.RussStation.Light;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.RussStation.Light;

public sealed class LightReplacerRecyclerSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LightReplacerRecyclerComponent, LightReplacerBulbReplacedEvent>(OnBulbReplaced);
        SubscribeLocalEvent<LightReplacerRecyclerComponent, LightReplacerPrintMessage>(OnPrintMessage);
        SubscribeLocalEvent<LightReplacerRecyclerComponent, ExaminedEvent>(OnExamined);
    }

    private void OnBulbReplaced(EntityUid uid, LightReplacerRecyclerComponent recycler, LightReplacerBulbReplacedEvent args)
    {
        if (!TryComp<LightBulbComponent>(args.BrokenBulb, out var bulb))
            return;

        if (bulb.State == LightBulbState.Normal)
            return;

        recycler.RecyclePoints += recycler.PointsPerRecycle;
        Dirty(uid, recycler);

        QueueDel(args.BrokenBulb);

        var msg = Loc.GetString("light-replacer-recycler-recycled",
            ("points", recycler.RecyclePoints),
            ("cost", recycler.PrintCost));
        _popup.PopupEntity(msg, uid, args.User);
    }

    private void OnPrintMessage(EntityUid uid, LightReplacerRecyclerComponent recycler, LightReplacerPrintMessage args)
    {
        var user = args.Actor;

        if (!recycler.PrintablePrototypes.Contains(args.PrototypeId))
            return;

        if (!_protoManager.HasIndex<EntityPrototype>(args.PrototypeId))
            return;

        if (recycler.RecyclePoints < recycler.PrintCost)
        {
            var msg = Loc.GetString("light-replacer-recycler-not-enough-points",
                ("points", recycler.RecyclePoints),
                ("cost", recycler.PrintCost));
            _popup.PopupEntity(msg, uid, user);
            return;
        }

        if (!TryComp<LightReplacerComponent>(uid, out var replacer))
            return;

        recycler.RecyclePoints -= recycler.PrintCost;
        Dirty(uid, recycler);

        var bulbEnt = Spawn(args.PrototypeId, Transform(uid).Coordinates);
        _container.Insert(bulbEnt, replacer.InsertedBulbs);

        var printMsg = Loc.GetString("light-replacer-recycler-printed",
            ("bulb", bulbEnt),
            ("points", recycler.RecyclePoints));
        _popup.PopupEntity(printMsg, uid, user);
    }

    private void OnExamined(EntityUid uid, LightReplacerRecyclerComponent recycler, ExaminedEvent args)
    {
        using (args.PushGroup(nameof(LightReplacerRecyclerComponent)))
        {
            args.PushMarkup(Loc.GetString("light-replacer-recycler-examine",
                ("points", recycler.RecyclePoints),
                ("cost", recycler.PrintCost)));
        }
    }
}
