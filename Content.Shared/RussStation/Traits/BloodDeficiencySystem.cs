using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared.RussStation.Traits;

/// <summary>
/// Sets BloodRefreshAmount to a negative value on MapInit, causing
/// the bloodstream system to slowly drain blood each tick instead of
/// regenerating it. Also hands the entity a starting pill canister so
/// they can self-treat: copper pills for copper-blooded species, iron
/// for everyone else.
/// </summary>
public sealed class BloodDeficiencySystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;

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

        bloodstream.BloodRefreshAmount = -component.BloodLossPerTick;

        if (!_net.IsServer)
            return;

        if (!TryComp<HandsComponent>(uid, out var hands))
            return;

        var pills = bloodstream.BloodReferenceSolution.ContainsPrototype(CopperBlood)
            ? CopperPills
            : IronPills;

        var pillEnt = Spawn(pills, Transform(uid).Coordinates);
        _hands.TryPickup(uid, pillEnt, checkActionBlocker: false, handsComp: hands);
    }
}
