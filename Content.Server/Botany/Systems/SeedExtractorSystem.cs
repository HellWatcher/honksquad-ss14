using Content.Server.Botany.Components;
using Content.Server.Popups;
using Content.Server.Power.EntitySystems;
using Content.Shared.Botany;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Storage.Components;
using Content.Shared.UserInterface;
using Robust.Server.Containers;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Random;

namespace Content.Server.Botany.Systems;

public sealed class SeedExtractorSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly BotanySystem _botanySystem = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSys = default!;
    [Dependency] private readonly ContainerSystem _container = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SeedExtractorComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<SeedExtractorComponent, BeforeActivatableUIOpenEvent>((u, c, _) => UpdateUserInterfaceState(u, c));
        SubscribeLocalEvent<SeedExtractorComponent, SeedExtractorTakeSeedMessage>(OnTakeSeedMessage);
        SubscribeLocalEvent<SeedExtractorComponent, GetDumpableVerbEvent>(OnGetDumpableVerb);
        SubscribeLocalEvent<SeedExtractorComponent, DumpEvent>(OnDump);
    }

    /// <summary>
    /// When the player uses an item on the extractor:
    /// - Produce → extract seeds as before (spawn packets, drop/hand to player).
    /// - Seed packet → store the packet inside the extractor.
    /// </summary>
    private void OnInteractUsing(EntityUid uid, SeedExtractorComponent seedExtractor, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!this.IsPowered(uid, EntityManager))
            return;

        // Storing a seed packet in the extractor
        if (TryComp(args.Used, out SeedComponent? _))
        {
            var seedContainer = _container.EnsureContainer<Container>(uid, seedExtractor.SeedContainerId);
            if (_container.Insert(args.Used, seedContainer))
            {
                args.Handled = true;
                UpdateUserInterfaceState(uid, seedExtractor);
            }
            return;
        }

        // Extracting seeds from produce (original behavior)
        if (!TryComp(args.Used, out ProduceComponent? produce))
            return;

        if (!_botanySystem.TryGetSeed(produce, out var seed) || seed.Seedless)
        {
            _popupSystem.PopupCursor(Loc.GetString("seed-extractor-component-no-seeds", ("name", args.Used)),
                args.User, PopupType.MediumCaution);
            return;
        }

        _popupSystem.PopupCursor(Loc.GetString("seed-extractor-component-interact-message", ("name", args.Used)),
            args.User, PopupType.Medium);

        QueueDel(args.Used);
        args.Handled = true;

        var amount = _random.Next(seedExtractor.BaseMinSeeds, seedExtractor.BaseMaxSeeds + 1);
        var coords = Transform(uid).Coordinates;

        var packetSeed = seed;
        if (amount > 1)
            packetSeed.Unique = false;

        for (var i = 0; i < amount; i++)
        {
            _botanySystem.SpawnSeedPacket(packetSeed, coords, args.User);
        }
    }

    /// <summary>
    /// Takes one seed packet from the group identified by <paramref name="args"/>.<see cref="SeedExtractorTakeSeedMessage.GroupKey"/>.
    /// </summary>
    private void OnTakeSeedMessage(EntityUid uid, SeedExtractorComponent component, SeedExtractorTakeSeedMessage args)
    {
        if (!_container.TryGetContainer(uid, component.SeedContainerId, out var seedContainer))
            return;

        foreach (var entity in seedContainer.ContainedEntities)
        {
            if (!TryComp(entity, out SeedComponent? seedComp))
                continue;

            if (!_botanySystem.TryGetSeed(seedComp, out var seed))
                continue;

            if (MakeGroupKey(seed) != args.GroupKey)
                continue;

            _container.Remove(entity, seedContainer);
            _hands.TryPickupAnyHand(args.Actor, entity);
            UpdateUserInterfaceState(uid, component);
            return;
        }
    }

    private void OnGetDumpableVerb(EntityUid uid, SeedExtractorComponent component, ref GetDumpableVerbEvent args)
    {
        if (this.IsPowered(uid, EntityManager))
            args.Verb = Loc.GetString("seed-extractor-dump-verb", ("unit", uid));
    }

    private void OnDump(EntityUid uid, SeedExtractorComponent component, ref DumpEvent args)
    {
        if (args.Handled)
            return;

        var seedContainer = _container.EnsureContainer<Container>(uid, component.SeedContainerId);
        var inserted = false;

        foreach (var entity in args.DumpQueue)
        {
            if (!HasComp<SeedComponent>(entity))
                continue;

            if (_container.Insert(entity, seedContainer))
                inserted = true;
        }

        if (inserted)
        {
            args.Handled = true;
            args.PlaySound = true;
            UpdateUserInterfaceState(uid, component);
        }
    }

    private void UpdateUserInterfaceState(EntityUid uid, SeedExtractorComponent component)
    {
        var seedDataList = new List<SeedExtractorSeedData>();

        if (_container.TryGetContainer(uid, component.SeedContainerId, out var seedContainer))
        {
            var groups = new Dictionary<string, (SeedData data, string displayName, int count)>();

            foreach (var entity in seedContainer.ContainedEntities)
            {
                if (!TryComp(entity, out SeedComponent? seedComp))
                    continue;

                if (!_botanySystem.TryGetSeed(seedComp, out var seed))
                    continue;

                var key = MakeGroupKey(seed);
                if (groups.TryGetValue(key, out var existing))
                    groups[key] = (existing.data, existing.displayName, existing.count + 1);
                else
                {
                    var packetName = Loc.GetString("botany-seed-packet-name",
                        ("seedName", Loc.GetString(seed.Name)),
                        ("seedNoun", Loc.GetString(seed.Noun)));
                    groups[key] = (seed, packetName, 1);
                }
            }

            foreach (var (key, (data, displayName, count)) in groups)
            {
                seedDataList.Add(new SeedExtractorSeedData
                {
                    DisplayName = displayName,
                    GroupKey = key,
                    PacketPrototype = data.PacketPrototype,
                    Count = count,
                    Potency = data.Potency,
                    Yield = data.Yield,
                    Endurance = data.Endurance,
                    Lifespan = data.Lifespan,
                    Maturation = data.Maturation,
                    Production = data.Production,
                });
            }
        }

        _uiSys.SetUiState(uid, SeedExtractorUiKey.Key, new SeedExtractorUpdateState(seedDataList));
    }

    /// <summary>
    /// Builds a composite key from all displayed stats so that seeds with any differing stat values
    /// are placed in separate groups regardless of sharing the same display name.
    /// </summary>
    private static string MakeGroupKey(SeedData seed)
    {
        return $"{seed.Name}|{seed.Potency:G}|{seed.Yield}|{seed.Endurance:G}|{seed.Lifespan:G}|{seed.Maturation:G}|{seed.Production:G}";
    }
}
