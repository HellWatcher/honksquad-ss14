using Content.Shared.RussStation.EscalatedGrab.Components;
using Content.Shared.RussStation.EscalatedGrab.Events;

namespace Content.Shared.RussStation.EscalatedGrab.Systems;

/// <summary>
/// Handles the Pushover quirk: multiplies grab resist time so it takes longer to break free.
/// </summary>
public sealed class SharedPushoverSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PushoverComponent, GrabResistAttemptEvent>(OnGrabResistAttempt);
    }

    private void OnGrabResistAttempt(EntityUid uid, PushoverComponent component, ref GrabResistAttemptEvent args)
    {
        args.ResistTime *= component.ResistTimeMultiplier;
    }
}
