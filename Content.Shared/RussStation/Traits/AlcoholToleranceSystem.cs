using static Content.Shared.Drunk.SharedDrunkSystem;

namespace Content.Shared.RussStation.Traits;

public sealed class AlcoholToleranceSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AlcoholToleranceComponent, DrunkEvent>(OnDrunk);
    }

    private void OnDrunk(EntityUid uid, AlcoholToleranceComponent component, ref DrunkEvent args)
    {
        args.Duration *= component.BoozeStrengthMultiplier;
    }
}
