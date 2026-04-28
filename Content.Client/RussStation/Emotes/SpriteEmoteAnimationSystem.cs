using Content.Shared.RussStation.Emotes;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;
using Robust.Shared.Maths;

namespace Content.Client.RussStation.Emotes;

/// HONK Plays a one-shot sprite rotation when <see cref="SpriteRotationAnimComponent"/> is
/// added (Flip / Spin emote). Reverts the sprite to its starting rotation on shutdown so a
/// late server-side removal doesn't leave the mob crooked.
public sealed class SpriteEmoteAnimationSystem : EntitySystem
{
    [Dependency] private readonly AnimationPlayerSystem _anim = default!;

    private const string AnimationKey = "russstation-sprite-emote";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SpriteRotationAnimComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<SpriteRotationAnimComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(Entity<SpriteRotationAnimComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        if (ent.Comp.Duration <= TimeSpan.Zero)
            return;

        var player = EnsureComp<AnimationPlayerComponent>(ent);
        if (_anim.HasRunningAnimation(ent.Owner, player, AnimationKey))
            _anim.Stop(ent.Owner, AnimationKey);

        var lenFloat = (float)ent.Comp.Duration.TotalSeconds;
        var startAngle = sprite.Rotation;
        var fullTurn = Angle.FromDegrees(360);

        var anim = new Animation
        {
            Length = ent.Comp.Duration,
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Rotation),
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(startAngle, 0f),
                        new AnimationTrackProperty.KeyFrame(startAngle + fullTurn * ent.Comp.Turns, lenFloat),
                    },
                    InterpolationMode = AnimationInterpolationMode.Linear,
                },
            },
        };

        _anim.Play((ent.Owner, player), anim, AnimationKey);
    }

    private void OnShutdown(Entity<SpriteRotationAnimComponent> ent, ref ComponentShutdown args)
    {
        if (_anim.HasRunningAnimation(ent.Owner, AnimationKey))
            _anim.Stop(ent.Owner, AnimationKey);
    }
}
