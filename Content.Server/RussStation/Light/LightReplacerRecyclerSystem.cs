using System.Linq;
using Content.Server.Light.Components;
using Content.Server.Light.EntitySystems;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Light.Components;
using Content.Shared.Popups;
using Content.Shared.RussStation.Light;
using Content.Shared.Tag;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.RussStation.Light;

public sealed class LightReplacerRecyclerSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;
    [Dependency] private readonly PoweredLightSystem _poweredLight = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    // Only plain glass shards recycle. Reinforced, plasma, uranium, and clockwork variants are
    // deliberately excluded because they're rarer or have higher-value refine paths elsewhere.
    private static readonly ProtoId<TagPrototype> GlassShardTag = "GlassShard";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LightReplacerRecyclerComponent, LightReplacerBulbReplacedEvent>(OnBulbReplaced);
        SubscribeLocalEvent<LightReplacerRecyclerComponent, LightReplacerBrokenBulbInsertEvent>(OnBrokenBulbInsert);
        SubscribeLocalEvent<LightReplacerRecyclerComponent, LightReplacerRecycleReplaceEvent>(OnRecycleReplace);
        SubscribeLocalEvent<LightReplacerRecyclerComponent, InteractUsingEvent>(OnInteractUsing, before: new[] { typeof(LightReplacerSystem) });
        SubscribeLocalEvent<LightReplacerRecyclerComponent, LightReplacerPrintMessage>(OnPrintMessage);
        SubscribeLocalEvent<LightReplacerRecyclerComponent, LightReplacerExtractMessage>(OnExtractMessage);
        SubscribeLocalEvent<LightReplacerRecyclerComponent, BoundUIOpenedEvent>(OnUIOpened);
        SubscribeLocalEvent<LightReplacerRecyclerComponent, EntInsertedIntoContainerMessage>(OnContainerChanged);
        SubscribeLocalEvent<LightReplacerRecyclerComponent, EntRemovedFromContainerMessage>(OnContainerChanged);
        SubscribeLocalEvent<LightReplacerRecyclerComponent, ContainerIsInsertingAttemptEvent>(OnContainerInserting);
        SubscribeLocalEvent<LightReplacerRecyclerComponent, ExaminedEvent>(OnExamined);
    }

    private void OnBulbReplaced(EntityUid uid, LightReplacerRecyclerComponent recycler, LightReplacerBulbReplacedEvent args)
    {
        if (!TryComp<LightBulbComponent>(args.BrokenBulb, out var bulb))
            return;

        if (bulb.State == LightBulbState.Normal)
            return;

        RecycleBulb(uid, recycler, args.BrokenBulb, args.User);
    }

    private void OnRecycleReplace(EntityUid uid, LightReplacerRecyclerComponent recycler, ref LightReplacerRecycleReplaceEvent args)
    {
        if (!TryComp<LightReplacerComponent>(uid, out var replacer))
            return;
        if (!TryComp<PoweredLightComponent>(args.FixtureUid, out var fixture))
            return;

        args.Handled = true;
        args.Success = RunRecycleReplace(uid, recycler, replacer, args.FixtureUid, fixture, args.FixtureBulbUid, args.UserUid);
    }

    // Flow: eat the broken bulb for a point, then pick a replacement, preferring an exact prototype
    // match from storage, then any stored bulb of the same BulbType, then a printed copy funded by
    // accumulated points (including the point just earned).
    private bool RunRecycleReplace(
        EntityUid replacerUid,
        LightReplacerRecyclerComponent recycler,
        LightReplacerComponent replacer,
        EntityUid fixtureUid,
        PoweredLightComponent fixture,
        EntityUid? brokenBulbUid,
        EntityUid? userUid)
    {
        var brokenProto = brokenBulbUid is { } bUid
            ? MetaData(bUid).EntityPrototype?.ID
            : null;

        var storageBulb = FindStorageBulb(replacer, fixture.BulbType, brokenProto);
        var projectedPoints = recycler.RecyclePoints + (brokenBulbUid != null ? recycler.PointsPerRecycle : 0);
        string? printProto = null;
        if (storageBulb == null && projectedPoints >= recycler.PrintCost)
            printProto = PickPrintPrototype(recycler, brokenProto, fixture.BulbType);

        if (storageBulb == null && printProto == null)
        {
            if (userUid != null)
            {
                var missing = Loc.GetString("comp-light-replacer-missing-light", ("light-replacer", replacerUid));
                _popup.PopupEntity(missing, replacerUid, userUid.Value);
            }
            return false;
        }

        if (brokenBulbUid is { } broken)
            RecycleBulb(replacerUid, recycler, broken, userUid);

        EntityUid replacement;
        var printed = false;
        if (storageBulb is { } stored)
        {
            if (!_container.Remove(stored, replacer.InsertedBulbs))
                return false;
            replacement = stored;
        }
        else
        {
            recycler.RecyclePoints -= recycler.PrintCost;
            Dirty(replacerUid, recycler);
            replacement = Spawn(printProto!, Transform(replacerUid).Coordinates);
            printed = true;
        }

        var replaced = _poweredLight.ReplaceBulb(fixtureUid, replacement, fixture);
        if (replaced)
        {
            _audio.PlayPvs(replacer.Sound, replacerUid);
            if (printed)
                _audio.PlayPvs(recycler.PrintSound, replacerUid);
        }
        PushState(replacerUid, recycler);
        return replaced;
    }

    private EntityUid? FindStorageBulb(LightReplacerComponent replacer, LightBulbType bulbType, string? preferredProto)
    {
        EntityUid? sameTypeFallback = null;
        foreach (var ent in replacer.InsertedBulbs.ContainedEntities)
        {
            if (!TryComp<LightBulbComponent>(ent, out var bulb) || bulb.Type != bulbType)
                continue;
            if (preferredProto != null && MetaData(ent).EntityPrototype?.ID == preferredProto)
                return ent;
            sameTypeFallback ??= ent;
        }
        return sameTypeFallback;
    }

    private string? PickPrintPrototype(LightReplacerRecyclerComponent recycler, string? preferredProto, LightBulbType bulbType)
    {
        if (preferredProto != null && recycler.PrintablePrototypes.Any(p => p.Id == preferredProto))
            return preferredProto;

        foreach (var protoId in recycler.PrintablePrototypes)
        {
            if (!_protoManager.TryIndex<EntityPrototype>(protoId, out var proto))
                continue;
            if (proto.TryGetComponent<LightBulbComponent>(out var bulbComp, EntityManager.ComponentFactory)
                && bulbComp.Type == bulbType)
                return protoId;
        }
        return null;
    }

    private void OnBrokenBulbInsert(EntityUid uid, LightReplacerRecyclerComponent recycler, ref LightReplacerBrokenBulbInsertEvent args)
    {
        if (!TryComp<LightBulbComponent>(args.BulbUid, out var bulb) || bulb.State == LightBulbState.Normal)
            return;

        RecycleBulb(uid, recycler, args.BulbUid, args.UserUid);
        args.Handled = true;
    }

    private void OnInteractUsing(EntityUid uid, LightReplacerRecyclerComponent recycler, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!_tag.HasTag(args.Used, GlassShardTag))
            return;

        RecycleBulb(uid, recycler, args.Used, args.User);
        args.Handled = true;
    }

    private void RecycleBulb(EntityUid uid, LightReplacerRecyclerComponent recycler, EntityUid scrapUid, EntityUid? user)
    {
        recycler.RecyclePoints += recycler.PointsPerRecycle;
        Dirty(uid, recycler);

        _audio.PlayPvs(recycler.RecycleSound, uid);
        QueueDel(scrapUid);

        if (user is { } userUid)
        {
            var msg = Loc.GetString("light-replacer-recycler-recycled",
                ("points", recycler.RecyclePoints),
                ("cost", recycler.PrintCost));
            _popup.PopupEntity(msg, uid, userUid);
        }

        PushState(uid, recycler);
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

        var bulbEnt = Spawn(args.PrototypeId, Transform(uid).Coordinates);

        if (!_container.Insert(bulbEnt, replacer.InsertedBulbs))
        {
            // The replacer refused the printed bulb (likely a slot cap imposed by another component).
            // Drop the spawned entity at the user and refund them the points they would've paid.
            QueueDel(bulbEnt);
            var full = Loc.GetString("light-replacer-recycler-full");
            _popup.PopupEntity(full, uid, user);
            return;
        }

        recycler.RecyclePoints -= recycler.PrintCost;
        Dirty(uid, recycler);

        _audio.PlayPvs(recycler.PrintSound, uid);

        var printMsg = Loc.GetString("light-replacer-recycler-printed",
            ("bulb", bulbEnt),
            ("points", recycler.RecyclePoints));
        _popup.PopupEntity(printMsg, uid, user);

        PushState(uid, recycler);
    }

    private void OnExtractMessage(EntityUid uid, LightReplacerRecyclerComponent recycler, LightReplacerExtractMessage args)
    {
        var user = args.Actor;

        if (!TryComp<LightReplacerComponent>(uid, out var replacer))
            return;

        EntityUid? target = null;
        foreach (var ent in replacer.InsertedBulbs.ContainedEntities)
        {
            if (MetaData(ent).EntityPrototype is { } proto && proto.ID == args.PrototypeId)
            {
                target = ent;
                break;
            }
        }

        if (target == null)
            return;

        if (!_container.Remove(target.Value, replacer.InsertedBulbs, destination: Transform(user).Coordinates))
            return;

        _hands.PickupOrDrop(user, target.Value);
        PushState(uid, recycler);
    }

    private void OnUIOpened(EntityUid uid, LightReplacerRecyclerComponent recycler, BoundUIOpenedEvent args)
    {
        if (args.UiKey is not LightReplacerRecyclerUiKey.Key)
            return;

        PushState(uid, recycler);
    }

    private void OnContainerChanged(EntityUid uid, LightReplacerRecyclerComponent recycler, ContainerModifiedMessage args)
    {
        if (args.Container.Owner != uid)
            return;

        PushState(uid, recycler);
    }

    private void OnContainerInserting(EntityUid uid, LightReplacerRecyclerComponent recycler, ContainerIsInsertingAttemptEvent args)
    {
        // Only guard the replacer's bulb storage; other containers on the same entity (e.g. hands
        // in the rare case the replacer is carried by something with hands) are not our business.
        if (args.Container.ID != "light_replacer_storage")
            return;

        if (args.Container.ContainedEntities.Count >= recycler.MaxStoredBulbs)
            args.Cancel();
    }

    private void PushState(EntityUid uid, LightReplacerRecyclerComponent recycler)
    {
        if (!_ui.IsUiOpen(uid, LightReplacerRecyclerUiKey.Key))
            return;

        if (!TryComp<LightReplacerComponent>(uid, out var replacer))
            return;

        var counts = new Dictionary<string, int>();
        foreach (var ent in replacer.InsertedBulbs.ContainedEntities)
        {
            var protoId = MetaData(ent).EntityPrototype?.ID;
            if (protoId == null)
                continue;
            counts[protoId] = counts.GetValueOrDefault(protoId) + 1;
        }

        var stored = counts
            .Select(kv => new LightReplacerStoredBulb(kv.Key, kv.Value))
            .OrderBy(e => e.ProtoId.Id)
            .ToList();

        var state = new LightReplacerRecyclerBoundUserInterfaceState(
            recycler.RecyclePoints,
            recycler.PrintCost,
            recycler.PointsPerRecycle,
            stored,
            recycler.PrintablePrototypes.ToList());

        _ui.SetUiState(uid, LightReplacerRecyclerUiKey.Key, state);
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
