using Content.Shared.Roles;
using Content.Shared.RussStation.Economy;
using Content.Shared.RussStation.Economy.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.RussStation.Economy;

/// <summary>
/// Deposits wages into player accounts on a regular interval based on job tier.
/// Tiers: Command (heads), Crew (standard), Lower (assistant/visitor).
/// </summary>
public sealed class PayrollSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly PlayerBalanceSystem _balance = default!;

    private static readonly ProtoId<DepartmentPrototype> CommandDepartment = "Command";

    private float _payrollInterval;
    private int _wageLower;
    private int _wageCrew;
    private int _wageCommand;

    private TimeSpan _nextPayroll;

    /// <summary>
    /// Jobs that receive lower-tier wages.
    /// </summary>
    private static readonly HashSet<string> LowerTierJobs = new()
    {
        "Passenger",
        "Visitor",
        "MedicalIntern",
        "TechnicalAssistant",
        "ResearchAssistant",
        "SecurityCadet",
        "ServiceWorker",
    };

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, EconomyCCVars.PayrollInterval, v => _payrollInterval = v, true);
        Subs.CVar(_cfg, EconomyCCVars.WageLower, v => _wageLower = v, true);
        Subs.CVar(_cfg, EconomyCCVars.WageCrew, v => _wageCrew = v, true);
        Subs.CVar(_cfg, EconomyCCVars.WageCommand, v => _wageCommand = v, true);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextPayroll)
            return;

        _nextPayroll = _timing.CurTime + TimeSpan.FromSeconds(_payrollInterval);
        IssuePayday();
    }

    private void IssuePayday()
    {
        var query = EntityQueryEnumerator<PlayerBalanceComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            var wage = GetWage(comp.JobId);
            if (wage <= 0)
                continue;

            _balance.AddBalance(uid, wage, comp);
        }
    }

    /// <summary>
    /// Determine the wage for a given job ID based on tier.
    /// </summary>
    public int GetWage(string? jobId)
    {
        if (string.IsNullOrEmpty(jobId))
            return _wageLower;

        // Check if it's a lower-tier job.
        if (LowerTierJobs.Contains(jobId))
            return _wageLower;

        // Check if the job is in the Command department.
        if (IsCommandJob(jobId))
            return _wageCommand;

        return _wageCrew;
    }

    private bool IsCommandJob(string jobId)
    {
        if (!_proto.TryIndex(CommandDepartment, out var command))
            return false;

        return command.Roles.Contains(jobId);
    }
}
