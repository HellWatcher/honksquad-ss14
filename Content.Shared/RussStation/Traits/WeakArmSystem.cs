using Content.Shared.Throwing;

namespace Content.Shared.RussStation.Traits;

public sealed class WeakArmSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WeakArmComponent, BeforeThrowEvent>(OnBeforeThrow);
    }

    private void OnBeforeThrow(EntityUid uid, WeakArmComponent component, ref BeforeThrowEvent args)
    {
        args.Direction *= component.ThrowMultiplier;
        args.ThrowSpeed *= component.ThrowMultiplier;
    }
}
