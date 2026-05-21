using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Kitchen.Components;
using Content.Shared.Popups;
using Content.Shared.RussStation.Skillchips.Systems;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Network;
using Robust.Shared.Random;

namespace Content.Shared.RussStation.Skillchips.Consumers;

/// <summary>
/// Shared trim handler for the Hedge 3 skillchip (<c>hedgetrimming</c> capability).
/// Lives in shared so the client also claims <see cref="InteractUsingEvent.Handled"/>
/// before <c>SecretStashSystem</c> runs; otherwise the client predicts the knife
/// being stashed and pops "You hide the knife..." even though the server later
/// rejects it. SS13 parallel: TRAIT_BONSAI on <c>/obj/item/kirbyplants</c>.
/// </summary>
public sealed class SharedHedgeTrimmingSystem : EntitySystem
{
    public const string HedgetrimmingTag = "hedgetrimming";

    [Dependency] private readonly SharedSkillchipSystem _skillchip = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();
        // Run before SecretStashSystem (also shared) so a sharp item from a chip
        // holder trims the plant instead of being stashed inside it.
        SubscribeLocalEvent<HedgeTrimmableComponent, InteractUsingEvent>(OnInteractUsing,
            before: new[] { typeof(SecretStashSystem) });
        SubscribeLocalEvent<HedgeTrimmableComponent, HedgeTrimDoAfterEvent>(OnTrimmed);
    }

    private void OnInteractUsing(Entity<HedgeTrimmableComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!HasComp<SharpComponent>(args.Used))
            return;

        if (!_skillchip.HasCapability(args.User, HedgetrimmingTag))
            return;

        // Claim the interaction so SecretStash bails on both client (prediction)
        // and server (reality), regardless of whether the do-after starts.
        args.Handled = true;

        var doAfter = new DoAfterArgs(
            EntityManager,
            args.User,
            ent.Comp.TrimDuration,
            new HedgeTrimDoAfterEvent(),
            ent.Owner,
            target: ent.Owner,
            used: args.Used)
        {
            BreakOnMove = true,
            NeedHand = true,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            _popup.PopupClient(Loc.GetString("skillchip-hedgetrimming-start"), ent.Owner, args.User);
    }

    private void OnTrimmed(Entity<HedgeTrimmableComponent> ent, ref HedgeTrimDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || ent.Comp.States.Count == 0)
            return;

        // State mutation only on the server: client prediction would pick a
        // different sprite and snap on the next server state.
        if (_net.IsServer)
        {
            ent.Comp.CurrentState = _random.Pick(ent.Comp.States);
            Dirty(ent);
        }

        _popup.PopupPredicted(Loc.GetString("skillchip-hedgetrimming-finish"), ent.Owner, args.User);
        args.Handled = true;
    }

}
