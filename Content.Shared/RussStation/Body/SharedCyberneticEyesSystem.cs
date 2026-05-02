using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Flash;

namespace Content.Shared.RussStation.Body;

/// <summary>
/// Advanced cybernetic eyes grant flash immunity to the host body. Lives in shared so
/// the cancellation is predicted on the wearer's client; otherwise the client briefly
/// renders the flash overlay and snaps back when the server's cancel arrives.
/// </summary>
public sealed class SharedCyberneticEyesSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BodyComponent, FlashAttemptEvent>(OnFlashAttempt);
    }

    private void OnFlashAttempt(EntityUid uid, BodyComponent body, ref FlashAttemptEvent args)
    {
        if (args.Cancelled || body.Organs == null)
            return;

        foreach (var organ in body.Organs.ContainedEntities)
        {
            if (HasComp<CyberneticEyesComponent>(organ))
            {
                args.Cancelled = true;
                return;
            }
        }
    }
}
