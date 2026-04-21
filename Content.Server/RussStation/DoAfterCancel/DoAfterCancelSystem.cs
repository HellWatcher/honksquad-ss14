using System.Linq;
using Content.Shared.DoAfter;
using Content.Shared.RussStation.DoAfterCancel;

namespace Content.Server.RussStation.DoAfterCancel;

/// <summary>
/// Handles <see cref="CancelAllDoAftersEvent"/>. Iterates the sender's
/// <see cref="DoAfterComponent"/> and cancels every DoAfter where the
/// sender is <see cref="DoAfterArgs.User"/>, i.e. actions the player
/// started themselves. Hostile DoAfters where the player is merely the
/// target live on a different user's component and are untouched.
/// </summary>
public sealed class DoAfterCancelSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<CancelAllDoAftersEvent>(OnCancelRequest);
    }

    private void OnCancelRequest(CancelAllDoAftersEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } player)
            return;

        if (!TryComp<DoAfterComponent>(player, out var comp))
            return;

        // Cancel() mutates the dictionary via Dirty / state replication,
        // and InternalCancel can raise events whose handlers may enqueue
        // more DoAfters; snapshot the pairs first.
        foreach (var (id, doAfter) in comp.DoAfters.ToArray())
        {
            if (doAfter.Cancelled || doAfter.Completed)
                continue;

            if (doAfter.Args.User != player)
                continue;

            _doAfter.Cancel(player, id, comp);
        }
    }
}
