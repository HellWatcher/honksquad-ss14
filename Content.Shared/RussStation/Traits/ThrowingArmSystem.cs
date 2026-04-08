using Content.Shared.Throwing;

namespace Content.Shared.RussStation.Traits;

public sealed class ThrowingArmSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ThrowingArmComponent, BeforeThrowEvent>(OnBeforeThrow);
    }

    private void OnBeforeThrow(EntityUid uid, ThrowingArmComponent component, ref BeforeThrowEvent args)
    {
        args.ThrowSpeed *= component.ThrowSpeedMultiplier;
    }
}
