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

public sealed class LightReplacerRecyclerSystem : SharedLightReplacerRecyclerSystem
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

    // How the recycler intends to source a replacement bulb for a fixture, in preference order.
    private enum BulbReplacementStrategy
    {
        // Nothing available: no stored bulb and not enough points to print.
        None,

        // A stored bulb whose prototype exactly matches the bulb being replaced.
        Exact,

        // A stored bulb of the right BulbType but a different prototype.
        TypeFallback,

        // A freshly printed bulb funded by accumulated recycle points.
        Print,
    }

    // The concrete replacement chosen by SelectReplacement. Exactly one payload is populated:
    // StorageBulb for Exact/TypeFallback, PrintProto for Print, neither for None.
    private readonly record struct BulbReplacementPlan(
        BulbReplacementStrategy Strategy,
        EntityUid? StorageBulb,
        string? PrintProto);

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

    // Flow: eat the broken bulb for a point, pick a replacement strategy (storage exact-match, then
    // same-type fallback, then a printed copy funded by the points including the one just earned),
    // then execute it. Strategy selection lives in SelectReplacement; this method just runs it.
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
        var projectedPoints = recycler.RecyclePoints + (brokenBulbUid != null ? recycler.PointsPerRecycle : 0);

        var plan = SelectReplacement(recycler, replacer, fixture, brokenProto, projectedPoints);

        if (plan.Strategy == BulbReplacementStrategy.None)
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
        var printed = plan.Strategy == BulbReplacementStrategy.Print;
        if (printed)
        {
            recycler.RecyclePoints -= recycler.PrintCost;
            Dirty(replacerUid, recycler);
            replacement = Spawn(plan.PrintProto!, Transform(replacerUid).Coordinates);
        }
        else
        {
            if (!_container.Remove(plan.StorageBulb!.Value, replacer.InsertedBulbs))
                return false;
            replacement = plan.StorageBulb.Value;
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

    // Picks the replacement source without mutating anything: storage first (exact prototype match
    // beats same-type fallback), then a fundable print, otherwise None.
    private BulbReplacementPlan SelectReplacement(
        LightReplacerRecyclerComponent recycler,
        LightReplacerComponent replacer,
        PoweredLightComponent fixture,
        string? brokenProto,
        int projectedPoints)
    {
        if (FindStorageBulb(replacer, fixture.BulbType, brokenProto) is { } storage)
        {
            var strategy = storage.Exact ? BulbReplacementStrategy.Exact : BulbReplacementStrategy.TypeFallback;
            return new BulbReplacementPlan(strategy, storage.Bulb, null);
        }

        if (projectedPoints >= recycler.PrintCost
            && PickPrintPrototype(recycler, brokenProto, fixture.BulbType) is { } printProto)
        {
            return new BulbReplacementPlan(BulbReplacementStrategy.Print, null, printProto);
        }

        return new BulbReplacementPlan(BulbReplacementStrategy.None, null, null);
    }

    private (EntityUid Bulb, bool Exact)? FindStorageBulb(LightReplacerComponent replacer, LightBulbType bulbType, string? preferredProto)
    {
        EntityUid? sameTypeFallback = null;
        foreach (var ent in replacer.InsertedBulbs.ContainedEntities)
        {
            if (!TryComp<LightBulbComponent>(ent, out var bulb) || bulb.Type != bulbType)
                continue;
            if (preferredProto != null && MetaData(ent).EntityPrototype?.ID == preferredProto)
                return (ent, true);
            sameTypeFallback ??= ent;
        }
        return sameTypeFallback is { } fb ? (fb, false) : null;
    }

    private string? PickPrintPrototype(LightReplacerRecyclerComponent recycler, string? preferredProto, LightBulbType bulbType)
    {
        if (preferredProto != null && IsPrintable(recycler, preferredProto))
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

        if (!IsPrintable(recycler, args.PrototypeId))
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

        // Extract pulls any stored bulb matching the requested prototype. Unlike printing, it is
        // intentionally not gated on PrintablePrototypes: players can manually insert bulbs the
        // recycler can't print, and must still be able to take those back out.
        if (FindStoredBulb(replacer, args.PrototypeId) is not { } target)
            return;

        if (!_container.Remove(target, replacer.InsertedBulbs, destination: Transform(user).Coordinates))
            return;

        _hands.PickupOrDrop(user, target);
        PushState(uid, recycler);
    }

    // Shared validator for the print paths (radial print + exact-match print fallback): the id must
    // be one the recycler advertises and must resolve to a real entity prototype.
    private bool IsPrintable(LightReplacerRecyclerComponent recycler, EntProtoId protoId)
    {
        return recycler.PrintablePrototypes.Contains(protoId)
            && _protoManager.HasIndex<EntityPrototype>(protoId);
    }

    private EntityUid? FindStoredBulb(LightReplacerComponent replacer, EntProtoId protoId)
    {
        foreach (var ent in replacer.InsertedBulbs.ContainedEntities)
        {
            if (MetaData(ent).EntityPrototype is { } proto && proto.ID == protoId)
                return ent;
        }
        return null;
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

        recycler.CachedInventory = null;
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

        var state = new LightReplacerRecyclerBoundUserInterfaceState(
            recycler.RecyclePoints,
            recycler.PrintCost,
            recycler.PointsPerRecycle,
            GetStoredInventory(recycler, replacer),
            recycler.PrintablePrototypes.ToList());

        _ui.SetUiState(uid, LightReplacerRecyclerUiKey.Key, state);
    }

    // Returns the per-prototype stored-bulb summary, rebuilding it only when the cache has been
    // invalidated by a storage container change (see OnContainerChanged).
    private List<LightReplacerStoredBulb> GetStoredInventory(LightReplacerRecyclerComponent recycler, LightReplacerComponent replacer)
    {
        if (recycler.CachedInventory is { } cached)
            return cached;

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

        recycler.CachedInventory = stored;
        return stored;
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
