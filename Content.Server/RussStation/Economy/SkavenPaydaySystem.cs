// HONK - Issue #481 P4: scale Skaven wages to 25% of the standard rate. Hooks the
// existing GetWageEvent that PayrollSystem raises before depositing a paycheck, the
// same mechanism Negotiator and Indebted traits use.

using Content.Shared.RussStation.Economy;

namespace Content.Server.RussStation.Economy;

public sealed class SkavenPaydaySystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SkavenPaydayComponent, GetWageEvent>(OnGetWage);
    }

    private void OnGetWage(EntityUid uid, SkavenPaydayComponent component, ref GetWageEvent args)
    {
        args.Wage = (int)(args.Wage * EconomyConstants.SkavenPaydayMultiplier);
    }
}
