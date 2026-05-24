using Content.Server.Radio;
using Content.Shared.RussStation.Hearing;

namespace Content.Server.RussStation.Hearing;

/// <summary>
/// Drops incoming radio messages on the floor for any receiver whose
/// <see cref="DeafableComponent.IsDeaf"/> flag is set. Lives server-side because
/// <see cref="RadioReceiveAttemptEvent"/> only exists on the server.
/// </summary>
public sealed class DeafRadioBlockSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DeafableComponent, RadioReceiveAttemptEvent>(OnRadioReceiveAttempt);
    }

    private void OnRadioReceiveAttempt(EntityUid uid, DeafableComponent component, ref RadioReceiveAttemptEvent args)
    {
        if (component.IsDeaf)
            args.Cancelled = true;
    }
}
