using Content.Server.Body.Systems;
using Content.Shared.Body.Components;
using Content.Shared.RussStation.Body;
using Content.Shared.StatusEffectNew;

namespace Content.Server.RussStation.Body;

/// <summary>
/// Handles the BreathingSuppressed status effect.
/// When active, intercepts inhaled gas events before lungs can process them,
/// causing the gas to be returned to the atmosphere and the body to suffocate naturally.
/// </summary>
public sealed class BreathingSuppressedSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BreathingSuppressedComponent, StatusEffectAppliedEvent>(OnApplied);
        SubscribeLocalEvent<BreathingSuppressedComponent, StatusEffectRemovedEvent>(OnRemoved);
        SubscribeLocalEvent<ActiveBreathingSuppressedComponent, InhaledGasEvent>(OnInhaled,
            before: [typeof(RespiratorSystem)]);
    }

    private void OnApplied(Entity<BreathingSuppressedComponent> ent, ref StatusEffectAppliedEvent args)
    {
        EnsureComp<ActiveBreathingSuppressedComponent>(args.Target);
    }

    private void OnRemoved(Entity<BreathingSuppressedComponent> ent, ref StatusEffectRemovedEvent args)
    {
        RemComp<ActiveBreathingSuppressedComponent>(args.Target);
    }

    private void OnInhaled(Entity<ActiveBreathingSuppressedComponent> ent, ref InhaledGasEvent args)
    {
        // Mark as handled so the gas is returned to atmosphere by RespiratorSystem.
        // Lungs will see Handled=true and skip processing, causing natural suffocation.
        args.Handled = true;
    }
}
