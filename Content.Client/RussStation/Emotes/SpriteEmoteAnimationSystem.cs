using CancellationTokenSource = System.Threading.CancellationTokenSource;
using Content.Shared.RussStation.Emotes;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client.RussStation.Emotes;

/// HONK Plays sprite emote animations on the client when the server raises
/// <see cref="SpriteEmoteAnimEvent"/>:
///   <see cref="SpriteEmoteKind.Flip"/> — animate <c>SpriteComponent.Rotation</c>;
///   <see cref="SpriteEmoteKind.Spin"/> — step through cardinal directions via DirectionOverride.
public sealed class SpriteEmoteAnimationSystem : EntitySystem
{
    [Dependency] private readonly AnimationPlayerSystem _anim = default!;

    private const string AnimationKey = "russstation-sprite-emote";

    // Per-entity cancellation for the Timer.Spawn-driven spin cycle, so a new (or spammed)
    // spin cancels the previous one's leftover step timers instead of letting them fight.
    private readonly Dictionary<EntityUid, CancellationTokenSource> _spinCancellers = new();

    // Counterclockwise as viewed on screen: S → E → N → W.
    private static readonly Direction[] SpinSequence =
    {
        Direction.South, Direction.East, Direction.North, Direction.West,
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<SpriteEmoteAnimEvent>(OnSpriteEmote);
    }

    private void OnSpriteEmote(SpriteEmoteAnimEvent ev)
    {
        if (!TryGetEntity(ev.Target, out var uid))
            return;

        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        if (ev.Duration <= TimeSpan.Zero)
            return;

        switch (ev.Kind)
        {
            case SpriteEmoteKind.Flip:
                PlayFlip(uid.Value, sprite, ev.Duration, ev.Amount);
                break;
            case SpriteEmoteKind.Spin:
                PlaySpin(uid.Value, sprite, ev.Duration, (int)ev.Amount);
                break;
        }
    }

    private void PlayFlip(EntityUid uid, SpriteComponent sprite, TimeSpan duration, float turns)
    {
        var player = EnsureComp<AnimationPlayerComponent>(uid);
        if (_anim.HasRunningAnimation(uid, player, AnimationKey))
            _anim.Stop(uid, AnimationKey);

        // Two gotchas drive this construction:
        //   1) Angle.Lerp uses ShortestDistance, so a single keyframe pair from 0 → 2π lerps
        //      a delta of 0 and the sprite never moves. Split each turn into 4 quarter-turn
        //      keyframes (90° per segment, well under the π wrap) so each lerp goes forward.
        //   2) The interpolator dispatches on the boxed runtime type of the first keyframe
        //      value; if a later keyframe is boxed as double (Angle has implicit operator
        //      double), the (Angle)b cast throws and the animation silently drops. Build
        //      every keyframe value via `new Angle(...)` so they're all boxed Angle.
        const int segmentsPerTurn = 4;
        var totalSegments = Math.Max(1, (int)Math.Round(turns * segmentsPerTurn));
        var startTheta = sprite.Rotation.Theta;
        var thetaPerSegment = Math.Tau * turns / totalSegments;
        var lenFloat = (float)duration.TotalSeconds;
        var timePerSegment = lenFloat / totalSegments;

        var track = new AnimationTrackComponentProperty
        {
            ComponentType = typeof(SpriteComponent),
            Property = nameof(SpriteComponent.Rotation),
            InterpolationMode = AnimationInterpolationMode.Linear,
        };
        track.KeyFrames.Add(new AnimationTrackProperty.KeyFrame(new Angle(startTheta), 0f));
        for (var i = 1; i <= totalSegments; i++)
        {
            track.KeyFrames.Add(new AnimationTrackProperty.KeyFrame(
                new Angle(startTheta + thetaPerSegment * i),
                timePerSegment));
        }

        var anim = new Animation
        {
            Length = duration,
            AnimationTracks = { track },
        };

        _anim.Play((uid, player), anim, AnimationKey);
    }

    private void PlaySpin(EntityUid uid, SpriteComponent sprite, TimeSpan duration, int steps)
    {
        if (steps <= 0)
            return;

        // Cancel any spin still cycling on this entity so its leftover timers don't fight this one.
        if (_spinCancellers.Remove(uid, out var previous))
        {
            previous.Cancel();
            previous.Dispose();
        }

        var cts = new CancellationTokenSource();
        _spinCancellers[uid] = cts;
        var token = cts.Token;

        // DirectionOverride/EnableDirectionOverride aren't [Animatable], so drive the cycle
        // off ticks via Timer.Spawn instead of an Animation track.
        var stepInterval = duration.TotalMilliseconds / steps;
        sprite.EnableDirectionOverride = true;
        for (var i = 0; i < steps; i++)
        {
            var dir = SpinSequence[i % SpinSequence.Length];
            var entity = uid;
            Timer.Spawn(TimeSpan.FromMilliseconds(stepInterval * i), () =>
            {
                if (TryComp<SpriteComponent>(entity, out var s))
                    s.DirectionOverride = dir;
            }, token);
        }
        Timer.Spawn(duration, () =>
        {
            if (TryComp<SpriteComponent>(uid, out var s))
                s.EnableDirectionOverride = false;

            // Only this spin's own token reaches here (a newer spin cancels it), so retire it.
            if (_spinCancellers.TryGetValue(uid, out var current) && current == cts)
            {
                _spinCancellers.Remove(uid);
                cts.Dispose();
            }
        }, token);
    }
}
