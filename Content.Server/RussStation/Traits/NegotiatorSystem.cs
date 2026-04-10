using Content.Shared.RussStation.Economy;
using Content.Shared.RussStation.Traits;

namespace Content.Server.RussStation.Traits;

public sealed class NegotiatorSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NegotiatorComponent, GetWageEvent>(OnGetWage);
    }

    private void OnGetWage(EntityUid uid, NegotiatorComponent component, ref GetWageEvent args)
    {
        args.Wage = (int)(args.Wage * component.WageMultiplier);
    }
}
