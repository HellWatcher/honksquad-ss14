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
/// Applies audio occlusion effects based on the local player's hearing state:
/// - Fully deaf: high occlusion (8f) via DeafableComponent.IsDeaf
/// - Impaired (basic cybernetic ears): partial occlusion (3.5f) via HearingImpairmentComponent
/// </summary>
public sealed class DeafAudioSystem : EntitySystem
{
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    private const float DeafOcclusion = 8f;

    private bool _isDeaf;
    private bool _isImpaired;
    private float _impairedOcclusion;
    private float _maxRayLength;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DeafableComponent, ComponentStartup>(OnDeafStartup);
        SubscribeLocalEvent<DeafableComponent, LocalPlayerAttachedEvent>(OnDeafAttached);
        SubscribeLocalEvent<DeafableComponent, LocalPlayerDetachedEvent>(OnDeafDetached);
        SubscribeLocalEvent<DeafableComponent, ComponentRemove>(OnDeafRemove);
        SubscribeLocalEvent<DeafableComponent, DeafnessChangedEvent>(OnDeafnessChanged);

        SubscribeLocalEvent<HearingImpairmentComponent, ComponentStartup>(OnImpairmentStartup);
        SubscribeLocalEvent<HearingImpairmentComponent, LocalPlayerAttachedEvent>(OnImpairmentAttached);
        SubscribeLocalEvent<HearingImpairmentComponent, LocalPlayerDetachedEvent>(OnImpairmentDetached);
        SubscribeLocalEvent<HearingImpairmentComponent, ComponentRemove>(OnImpairmentRemove);

        Subs.CVar(_cfg, Robust.Shared.CVars.AudioRaycastLength, v => _maxRayLength = v, true);
    }

    // ── Deafness handlers ────────────────────────────────────────────────────

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

    // ── Impairment handlers ───────────────────────────────────────────────────

    private void OnImpairmentStartup(EntityUid uid, HearingImpairmentComponent comp, ComponentStartup args)
    {
        if (uid == _player.LocalEntity)
            SetImpaired(true, comp.OcclusionBonus);
    }

    private void OnImpairmentAttached(EntityUid uid, HearingImpairmentComponent comp, LocalPlayerAttachedEvent args)
    {
        SetImpaired(true, comp.OcclusionBonus);
    }

    private void OnImpairmentDetached(EntityUid uid, HearingImpairmentComponent comp, LocalPlayerDetachedEvent args)
    {
        SetImpaired(false, 0f);
    }

    private void OnImpairmentRemove(EntityUid uid, HearingImpairmentComponent comp, ComponentRemove args)
    {
        if (uid == _player.LocalEntity)
            SetImpaired(false, 0f);
    }

    // ── State management ──────────────────────────────────────────────────────

    private void SetDeaf(bool deaf)
    {
        if (_isDeaf == deaf)
            return;

        var wasActive = _isDeaf || _isImpaired;
        _isDeaf = deaf;
        var isActive = _isDeaf || _isImpaired;

        UpdateOcclusionDelegate(wasActive, isActive);
    }

    private void SetImpaired(bool impaired, float occlusion)
    {
        if (_isImpaired == impaired)
            return;

        var wasActive = _isDeaf || _isImpaired;
        _isImpaired = impaired;
        _impairedOcclusion = occlusion;
        var isActive = _isDeaf || _isImpaired;

        UpdateOcclusionDelegate(wasActive, isActive);
    }

    private void UpdateOcclusionDelegate(bool wasActive, bool isActive)
    {
        if (isActive && !wasActive)
            _audio.GetOcclusionOverride += OcclusionOverride;
        else if (!isActive && wasActive)
            _audio.GetOcclusionOverride -= OcclusionOverride;
    }

    // Mirrors the engine's baseline occlusion raycast, then adds the appropriate
    // bonus on top so wall muffling still stacks.
    // Keep in sync with RobustToolbox/Robust.Client/Audio/AudioSystem.cs GetOcclusion during rebases.
    private float OcclusionOverride(MapCoordinates listener, Vector2 delta, float distance, EntityUid? ignoredEnt)
    {
        float occlusion = 0;

        if (distance > 0.1f)
        {
            var rayLength = MathF.Min(distance, _maxRayLength);
            var ray = new CollisionRay(listener.Position, delta / distance, _audio.OcclusionCollisionMask);
            occlusion = _physics.IntersectRayPenetration(listener.MapId, ray, rayLength, ignoredEnt);
        }

        // Full deafness takes priority over impairment
        var bonus = _isDeaf ? DeafOcclusion : _impairedOcclusion;
        return occlusion + bonus;
    }
}
