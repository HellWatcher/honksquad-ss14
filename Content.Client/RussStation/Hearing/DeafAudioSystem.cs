using System.Numerics;
using Content.Shared.RussStation.Hearing;
using Content.Shared.RussStation.Hearing.Systems;
using Robust.Client.Audio;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;

namespace Content.Client.RussStation.Hearing;

/// <summary>
/// Applies an audio low-pass muffling effect when the local player is deaf
/// by injecting extra occlusion into the engine's audio processing.
/// </summary>
public sealed class DeafAudioSystem : EntitySystem
{
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    /// <summary>
    /// Extra occlusion added to every audio source when deaf.
    /// Drives the OpenAL EFX lowpass filter, muffling all sounds.
    /// </summary>
    private const float DeafOcclusion = 8f;

    private bool _isDeaf;
    private float _maxRayLength;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DeafableComponent, LocalPlayerAttachedEvent>(OnAttached);
        SubscribeLocalEvent<DeafableComponent, LocalPlayerDetachedEvent>(OnDetached);
        SubscribeLocalEvent<DeafableComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<DeafableComponent, DeafnessChangedEvent>(OnDeafnessChanged);

        Subs.CVar(_cfg, Robust.Shared.CVars.AudioRaycastLength, v => _maxRayLength = v, true);
    }

    private void OnAttached(EntityUid uid, DeafableComponent comp, LocalPlayerAttachedEvent args)
    {
        SetDeaf(comp.IsDeaf);
    }

    private void OnDetached(EntityUid uid, DeafableComponent comp, LocalPlayerDetachedEvent args)
    {
        SetDeaf(false);
    }

    private void OnRemove(EntityUid uid, DeafableComponent comp, ComponentRemove args)
    {
        SetDeaf(false);
    }

    private void OnDeafnessChanged(EntityUid uid, DeafableComponent comp, ref DeafnessChangedEvent args)
    {
        SetDeaf(args.Deaf);
    }

    private void SetDeaf(bool deaf)
    {
        if (_isDeaf == deaf)
            return;

        _isDeaf = deaf;

        if (deaf)
            _audio.GetOcclusionOverride += OcclusionOverride;
        else
            _audio.GetOcclusionOverride -= OcclusionOverride;
    }

    private float OcclusionOverride(MapCoordinates listener, Vector2 delta, float distance, EntityUid? ignoredEnt)
    {
        float occlusion = 0;

        if (distance > 0.1f)
        {
            var rayLength = MathF.Min(distance, _maxRayLength);
            var ray = new CollisionRay(listener.Position, delta / distance, _audio.OcclusionCollisionMask);
            occlusion = _physics.IntersectRayPenetration(listener.MapId, ray, rayLength, ignoredEnt);
        }

        return occlusion + DeafOcclusion;
    }
}
