using System.Numerics;
using Content.Shared.RussStation.Hearing;
using Content.Shared.RussStation.Hearing.Systems;
using Robust.Client.Audio;
using Robust.Client.Player;
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
    [Dependency] private readonly IPlayerManager _player = default!;

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

        SubscribeLocalEvent<DeafableComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<DeafableComponent, LocalPlayerAttachedEvent>(OnAttached);
        SubscribeLocalEvent<DeafableComponent, LocalPlayerDetachedEvent>(OnDetached);
        SubscribeLocalEvent<DeafableComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<DeafableComponent, DeafnessChangedEvent>(OnDeafnessChanged);

        Subs.CVar(_cfg, Robust.Shared.CVars.AudioRaycastLength, v => _maxRayLength = v, true);
    }

    private void OnStartup(EntityUid uid, DeafableComponent comp, ComponentStartup args)
    {
        // Handle the case where DeafableComponent is added to an entity that is already
        // the local player (e.g. cloning, mid-round trait toggles, admin verbs).
        // LocalPlayerAttachedEvent won't fire again for an already-attached entity.
        if (uid == _player.LocalEntity)
            SetDeaf(comp.IsDeaf);
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

        // GetOcclusionOverride is asserted HasSingleTarget inside the engine
        // (RobustToolbox/Robust.Client/Audio/AudioSystem.cs GetOcclusion), so no
        // other content system can subscribe at the same time or debug builds crash.
        if (deaf)
            _audio.GetOcclusionOverride += OcclusionOverride;
        else
            _audio.GetOcclusionOverride -= OcclusionOverride;
    }

    // Mirrors the engine's baseline occlusion raycast in
    // RobustToolbox/Robust.Client/Audio/AudioSystem.cs GetOcclusion, then adds
    // DeafOcclusion on top so wall muffling still stacks. Keep this in sync
    // during upstream rebases if the engine's raycast shape changes.
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
