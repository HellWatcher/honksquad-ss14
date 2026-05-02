using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.RussStation.Carrying.Components;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.RussStation.EscalatedGrab.Components;

/// <summary>
/// Added to a puller entity to track escalated grab state against a target.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GrabStateComponent : Component
{
    /// <summary>
    /// Entity being held in an escalated grab (physically restrained at a tier).
    /// One of three independent "holding" relationships on a mob, alongside
    /// <see cref="PullerComponent.Pulling"/> (standard drag-pull) and
    /// <see cref="CarrierComponent.Carrying"/> (fireman carry / lift).
    /// A single entity may legitimately participate in more than one at the same time.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid Target;

    [DataField, AutoNetworkedField]
    public GrabStage Stage = GrabStage.Pull;

    /// <summary>
    /// Active escalation do-after, if any.
    /// </summary>
    [DataField, AutoNetworkedField]
    public DoAfterId? EscalateDoAfter;

    /// <summary>
    /// Active resist do-after on the target, if any.
    /// </summary>
    [DataField, AutoNetworkedField]
    public DoAfterId? ResistDoAfter;

    /// <summary>
    /// Accumulator for choke stamina damage ticks.
    /// </summary>
    [DataField]
    public float ChokeDamageAccumulator;

    /// <summary>
    /// Seconds between choke damage ticks.
    /// </summary>
    [DataField]
    public float ChokeTickInterval = EscalatedGrabConstants.ChokeTickInterval;

    /// <summary>
    /// Stamina damage dealt per choke tick.
    /// </summary>
    [DataField]
    public float ChokeStaminaPerTick = EscalatedGrabConstants.ChokeStaminaPerTick;

    /// <summary>
    /// Minimum single-hit damage to drop one grab stage.
    /// </summary>
    [DataField]
    public FixedPoint2 DamageDropThreshold = FixedPoint2.New(DefaultDamageDropThreshold);

    /// <summary>
    /// Default value for <see cref="DamageDropThreshold"/>. Also used by the vanilla-pull
    /// damage break in <c>SharedEscalatedGrabSystem</c> for entities with no GrabState.
    /// </summary>
    public const int DefaultDamageDropThreshold = 15;

    /// <summary>
    /// Do-after durations for each escalation step (index = target stage).
    /// </summary>
    public static readonly TimeSpan[] EscalationTimes =
    [
        TimeSpan.Zero,             // Pull (never used, pull is instant)
        TimeSpan.FromSeconds(2),   // Grab
        TimeSpan.FromSeconds(3),   // Aggressive
        TimeSpan.FromSeconds(4),   // Choke
    ];

    /// <summary>
    /// Resist do-after durations by current stage.
    /// </summary>
    public static readonly TimeSpan[] ResistTimes =
    [
        TimeSpan.Zero,             // Pull (instant release)
        TimeSpan.FromSeconds(3),   // Grab
        TimeSpan.FromSeconds(5),   // Aggressive
        TimeSpan.FromSeconds(6),   // Choke
    ];

    /// <summary>
    /// Walk/sprint speed multipliers for the puller at each stage.
    /// </summary>
    public static readonly float[] PullerSpeedModifiers =
    [
        0.95f, // Pull (existing upstream value)
        0.85f, // Grab
        0.75f, // Aggressive
        0.65f, // Choke
    ];

    /// <summary>
    /// Strip time multipliers applied to the target at each stage.
    /// Lower values mean faster stripping.
    /// </summary>
    public static readonly float[] StripTimeModifiers =
    [
        1.0f, // Pull (no change)
        0.8f, // Grab
        0.5f, // Aggressive
        0.3f, // Choke
    ];
}
