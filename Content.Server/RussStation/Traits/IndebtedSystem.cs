using Content.Shared.RussStation.Economy;
using Content.Shared.RussStation.Traits;

namespace Content.Server.RussStation.Traits;

public sealed class IndebtedSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<IndebtedComponent, GetWageEvent>(OnGetWage);
    }

    private void OnGetWage(EntityUid uid, IndebtedComponent component, ref GetWageEvent args)
    {
        args.Wage = (int)(args.Wage * component.WageMultiplier);
    }
}
