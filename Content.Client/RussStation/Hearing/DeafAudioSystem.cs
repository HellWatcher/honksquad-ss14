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
/// Applies audio occlusion to muffle sounds for the local player while their
/// <see cref="DeafableComponent.IsDeaf"/> is set. Partial-occlusion sources like
/// hearing impairment from cybernetic ears live in their own systems and stack on
/// top via the same <see cref="AudioSystem.GetOcclusionOverride"/> delegate hook.
/// </summary>
public sealed partial class DeafAudioSystem : EntitySystem
{
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPlayerManager _player = default!;

    private const float DeafOcclusion = 8f;

    private bool _isDeaf;
    private float _maxRayLength;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DeafableComponent, ComponentStartup>(OnDeafStartup);
        SubscribeLocalEvent<DeafableComponent, LocalPlayerAttachedEvent>(OnDeafAttached);
        SubscribeLocalEvent<DeafableComponent, LocalPlayerDetachedEvent>(OnDeafDetached);
        SubscribeLocalEvent<DeafableComponent, ComponentRemove>(OnDeafRemove);
        SubscribeLocalEvent<DeafableComponent, DeafnessChangedEvent>(OnDeafnessChanged);

        Subs.CVar(_cfg, Robust.Shared.CVars.AudioRaycastLength, v => _maxRayLength = v, true);
    }

    private void OnDeafStartup(EntityUid uid, DeafableComponent comp, ComponentStartup args)
    {
        if (uid == _player.LocalEntity)
            SetDeaf(comp.IsDeaf);
    }

    private void OnDeafAttached(EntityUid uid, DeafableComponent comp, LocalPlayerAttachedEvent args)
    {
        SetDeaf(comp.IsDeaf);
    }

    private void OnDeafDetached(EntityUid uid, DeafableComponent comp, LocalPlayerDetachedEvent args)
    {
        SetDeaf(false);
    }

    private void OnDeafRemove(EntityUid uid, DeafableComponent comp, ComponentRemove args)
    {
        if (uid == _player.LocalEntity)
            SetDeaf(false);
    }

    private void OnDeafnessChanged(EntityUid uid, DeafableComponent comp, ref DeafnessChangedEvent args)
    {
        if (uid == _player.LocalEntity)
            SetDeaf(args.Deaf);
    }

    private void SetDeaf(bool deaf)
    {
        if (_isDeaf == deaf)
            return;

        _isDeaf = deaf;

        if (_isDeaf)
            _audio.GetOcclusionOverride += OcclusionOverride;
        else
            _audio.GetOcclusionOverride -= OcclusionOverride;
    }

    // Mirrors the engine's baseline occlusion raycast, then adds the deaf bonus on top so
    // wall muffling still stacks. Keep in sync with
    // RobustToolbox/Robust.Client/Audio/AudioSystem.cs GetOcclusion during rebases.
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
