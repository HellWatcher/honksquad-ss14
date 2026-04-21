using Content.Shared.DoAfter;
using Content.Shared.RussStation.DoAfterCancel;
using Robust.Client.Player;

namespace Content.Client.RussStation.DoAfterCancel;

/// <summary>
/// Client hook for honksquad #513. <see cref="TryRequestCancel"/> checks
/// whether the local player has any active DoAfters and, if so, asks the
/// server to cancel them all; the server is authoritative and broadcasts
/// the result. Returns true when a request was sent so callers (the
/// Escape key handler) can consume the input.
/// </summary>
public sealed class DoAfterCancelRequestSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;

    public bool TryRequestCancel()
    {
        if (_player.LocalEntity is not { } player)
            return false;

        if (!TryComp<DoAfterComponent>(player, out var comp))
            return false;

        var hasActive = false;
        foreach (var doAfter in comp.DoAfters.Values)
        {
            if (doAfter.Cancelled || doAfter.Completed)
                continue;
            if (doAfter.Args.User != player)
                continue;
            hasActive = true;
            break;
        }

        if (!hasActive)
            return false;

        RaiseNetworkEvent(new CancelAllDoAftersEvent());
        return true;
    }
}
