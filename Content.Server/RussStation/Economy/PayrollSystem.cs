using Content.Shared.CartridgeLoader;
using Content.Shared.PDA;
using Content.Shared.Popups;
using Content.Shared.Mind;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Content.Shared.RussStation.Economy;
using Content.Shared.RussStation.Economy.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
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
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly PlayerBalanceSystem _balance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;

    private static readonly ProtoId<DepartmentPrototype> CommandDepartment = "Command";

    private float _payrollInterval;
    private int _wageLower;
    private int _wageCrew;
    private int _wageCommand;

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

        SubscribeLocalEvent<PlayerBalanceComponent, ComponentStartup>(OnBalanceStartup);
        SubscribeLocalEvent<RoleAddedEvent>(OnRoleAdded);
    }

    private void OnRoleAdded(RoleAddedEvent args)
    {
        if (args.Mind.CurrentEntity is not { } mob)
            return;

        if (!TryComp<PlayerBalanceComponent>(mob, out var comp))
            return;

        if (!_jobs.MindTryGetJobId(args.MindId, out var jobId))
            return;

        comp.JobId = jobId!.Value;
    }

    private void OnBalanceStartup(EntityUid uid, PlayerBalanceComponent comp, ComponentStartup args)
    {
        // First paycheck after one full interval, staggered +/- 60s so not everyone pays at once.
        var offset = _payrollInterval + _random.NextFloat(-60f, 60f);
        comp.NextPayroll = _timing.CurTime + TimeSpan.FromSeconds(offset);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var interval = TimeSpan.FromSeconds(_payrollInterval);
        var query = EntityQueryEnumerator<PlayerBalanceComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            if (now < comp.NextPayroll)
                continue;

            comp.NextPayroll = now + interval;

            var wage = GetWage(comp.JobId);
            if (wage <= 0)
                continue;

            var wageEv = new GetWageEvent(wage);
            RaiseLocalEvent(uid, ref wageEv);
            wage = wageEv.Wage;

            if (wage <= 0)
                continue;

            _balance.AddBalance(uid, wage, comp, Loc.GetString("transaction-payroll"));
            var newBalance = _balance.GetBalance(uid, comp);
            _popup.PopupEntity(Loc.GetString("payroll-received", ("wage", wage), ("balance", newBalance)), uid, uid);

            if (!comp.PaycheckMuted)
                PlayPdaChime(uid);
        }
    }

    private static readonly SoundSpecifier PaycheckSound =
        new SoundPathSpecifier("/Audio/Machines/chime.ogg", AudioParams.Default.WithVolume(-4f));

    private void PlayPdaChime(EntityUid mob)
    {
        // Find a PDA held by this mob with an ID inserted and play the sound from it.
        var pdaQuery = EntityQueryEnumerator<PdaComponent, CartridgeLoaderComponent>();
        while (pdaQuery.MoveNext(out var loaderUid, out var pda, out _))
        {
            if (Transform(loaderUid).ParentUid == mob && pda.ContainedId != null)
            {
                _audio.PlayPvs(PaycheckSound, loaderUid);
                return;
            }
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
