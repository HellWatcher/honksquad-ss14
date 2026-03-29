using Content.Server.Botany.Components;
using Content.Server.Popups;
using Content.Server.Power.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Random;

// HONK START
using Content.Shared.RussStation.Botany;
using Content.Shared.RussStation.Botany.Components;
using Content.Shared.RussStation.Botany.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
// HONK END

namespace Content.Server.Botany.Systems;

public sealed class SeedExtractorSystem : SharedSeedExtractorSystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly BotanySystem _botanySystem = default!;

    // HONK START
    [Dependency] private readonly UserInterfaceSystem _uiSys = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    // HONK END

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SeedExtractorComponent, InteractUsingEvent>(OnInteractUsing);

        // HONK START
        SubscribeLocalEvent<SeedExtractorComponent, BeforeActivatableUIOpenEvent>((u, c, _) => UpdateUserInterfaceState(u, c));
        SubscribeLocalEvent<SeedExtractorComponent, SeedExtractorTakeSeedMessage>(OnTakeSeedMessage);
        SubscribeLocalEvent<SeedExtractorComponent, EntInsertedIntoContainerMessage>(OnContainerChanged);
        SubscribeLocalEvent<SeedExtractorComponent, EntRemovedFromContainerMessage>(OnContainerChanged);
        // HONK END
    }

    ///
    /// <summary>
    /// When the player uses an item on the extractor:
    /// - Produce → extract seeds as before (spawn packets, drop/hand to player).
    /// - Seed packet → store the packet inside the extractor.
    /// </summary>
    private void OnInteractUsing(EntityUid uid, SeedExtractorComponent seedExtractor, InteractUsingEvent args)
    {
        if (!this.IsPowered(uid, EntityManager))
            return;

        // HONK START
        if (args.Handled)
            return;

        // Storing a seed packet in the extractor
        if (TryComp(args.Used, out SeedComponent? _))
        {
            var seedContainer = Container.EnsureContainer<Container>(uid, seedExtractor.SeedContainerId);
            if (Container.Insert(args.Used, seedContainer))
                args.Handled = true;
            return;
        }
        // HONK END

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

    // HONK START
    /// <summary>
    /// Takes one seed packet from the group identified by <paramref name="args"/>.<see cref="SeedExtractorTakeSeedMessage.GroupKey"/>.
    /// </summary>
    private void OnTakeSeedMessage(EntityUid uid, SeedExtractorComponent component, SeedExtractorTakeSeedMessage args)
    {
        if (!Container.TryGetContainer(uid, component.SeedContainerId, out var seedContainer))
            return;

        foreach (var entity in seedContainer.ContainedEntities)
        {
            if (!TryComp(entity, out SeedComponent? seedComp))
                continue;

            if (!_botanySystem.TryGetSeed(seedComp, out var seed))
                continue;

            if (MakeGroupKey(seed) != args.GroupKey)
                continue;

            Container.Remove(entity, seedContainer);
            _hands.TryPickupAnyHand(args.Actor, entity);
            return;
        }
    }

    private void OnContainerChanged(EntityUid uid, SeedExtractorComponent component, ContainerModifiedMessage args)
    {
        if (args.Container.ID != component.SeedContainerId)
            return;

        UpdateUserInterfaceState(uid, component);
    }

    private void UpdateUserInterfaceState(EntityUid uid, SeedExtractorComponent component)
    {
        var seedDataList = new List<SeedExtractorSeedData>();

        if (Container.TryGetContainer(uid, component.SeedContainerId, out var seedContainer))
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
    // HONK END
}
