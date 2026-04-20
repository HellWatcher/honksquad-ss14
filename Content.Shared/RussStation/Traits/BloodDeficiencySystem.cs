using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared.RussStation.Traits;

/// <summary>
/// Sets BloodRefreshAmount to a negative value on MapInit, causing
/// the bloodstream system to slowly drain blood each tick instead of
/// regenerating it. Also gives the entity a starter pill canister so
/// they can self-treat: copper pills for copper-blooded species, iron
/// for everyone else. Prefers the backpack, falls back to hand.
/// </summary>
public sealed class BloodDeficiencySystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;

    private static readonly EntProtoId IronPills = "PillCanisterIron";
    private static readonly EntProtoId CopperPills = "PillCanisterCopper";
    private static readonly ProtoId<ReagentPrototype> CopperBlood = "CopperBlood";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BloodDeficiencyComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(EntityUid uid, BloodDeficiencyComponent component, MapInitEvent args)
    {
        if (!TryComp<BloodstreamComponent>(uid, out var bloodstream))
            return;

#pragma warning disable HONK0003 // BloodstreamComponent grants this system as an [Access] friend (HONK-marked on the upstream file); write is sanctioned fork drift.
        bloodstream.BloodRefreshAmount = -component.BloodLossPerTick;
#pragma warning restore HONK0003

        if (!_net.IsServer)
            return;

        var pills = bloodstream.BloodReferenceSolution.ContainsPrototype(CopperBlood)
            ? CopperPills
            : IronPills;

        var pillEnt = Spawn(pills, Transform(uid).Coordinates);

        if (_inventory.TryGetSlotEntity(uid, "back", out var backpack)
            && HasComp<StorageComponent>(backpack)
            && _storage.Insert(backpack.Value, pillEnt, out _, playSound: false))
        {
            return;
        }

        if (TryComp<HandsComponent>(uid, out var hands))
            _hands.TryPickup(uid, pillEnt, checkActionBlocker: false, handsComp: hands);
    }
}
