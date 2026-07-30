using Content.Server.Body.Systems;
using Content.Shared.Alert;
using Content.Shared.Examine;
using Content.Shared.Medical;
using Content.Shared.RussStation.Body;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Content.Shared.Stunnable;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.RussStation.Body;

/// <summary>
/// Handles the CardiacArrest status effect.
/// When active, stuns the target and rapidly drains respirator saturation
/// to simulate oxygen not reaching cells due to the heart not pumping blood.
/// Lungs still work, but can't keep up with the drain.
/// </summary>
public sealed partial class CardiacArrestSystem : EntitySystem
{
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private RespiratorSystem _respirator = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly EntProtoId EffectProto = "StatusEffectCardiacArrest";
    private static readonly ProtoId<AlertPrototype> CardiacArrestAlert = "CardiacArrest";

    /// <summary>
    /// Extra saturation drained per second. Normal drain is ~1/s, so 3/s extra
    /// means lungs can't compensate and the entity suffocates.
    /// </summary>
    private const float DrainPerSecond = 3f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CardiacArrestComponent, StatusEffectAppliedEvent>(OnApplied);
        SubscribeLocalEvent<CardiacArrestComponent, StatusEffectRemovedEvent>(OnRemoved);
        SubscribeLocalEvent<ActiveCardiacArrestComponent, TargetDefibrillatedEvent>(OnDefibrillated);
        SubscribeLocalEvent<ActiveCardiacArrestComponent, ExaminedEvent>(OnExamined);
    }

    private void OnApplied(Entity<CardiacArrestComponent> ent, ref StatusEffectAppliedEvent args)
    {
        if (_timing.ApplyingState)
            return;

        EnsureComp<ActiveCardiacArrestComponent>(args.Target);
        _alerts.ShowAlert(args.Target, CardiacArrestAlert);

        if (TryComp<StatusEffectComponent>(ent, out var effect))
            _stun.TryAddParalyzeDuration(args.Target, effect.Duration);
    }

    private void OnRemoved(Entity<CardiacArrestComponent> ent, ref StatusEffectRemovedEvent args)
    {
        RemComp<ActiveCardiacArrestComponent>(args.Target);
        _alerts.ClearAlert(args.Target, CardiacArrestAlert);
    }

    private void OnDefibrillated(Entity<ActiveCardiacArrestComponent> ent, ref TargetDefibrillatedEvent args)
    {
        _statusEffects.TryRemoveStatusEffect(ent, EffectProto);
    }

    private void OnExamined(Entity<ActiveCardiacArrestComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("cardiac-arrest-examine", ("target", ent.Owner)));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var drain = -DrainPerSecond * frameTime;
        var query = EntityQueryEnumerator<ActiveCardiacArrestComponent>();

        while (query.MoveNext(out var uid, out _))
        {
            _respirator.UpdateSaturation(uid, drain);
        }
    }
}
