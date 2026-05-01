using Content.Shared.Actions;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Light.Components;
using Content.Shared.RussStation.Body;
using Robust.Shared.GameObjects;

namespace Content.Server.RussStation.Body;

/// <summary>
/// Wires advanced cybernetic eyes (which carry a <see cref="UnpoweredFlashlightComponent"/> and a cone
/// <see cref="SharedPointLightComponent"/> in YAML) up to the body that hosts them. Exact same idea as
/// equipping a flashlight, but on organ insertion: grant the toggle action to the body, mark the body's
/// organ container non-occluding so the cone actually reaches the world, and clean up on removal.
/// </summary>
public sealed class CyberneticEyeFlashlightSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedPointLightSystem _light = default!;
    [Dependency] private readonly BodySystem _body = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CyberneticEyesComponent, OrganGotInsertedEvent>(OnInserted);
        SubscribeLocalEvent<CyberneticEyesComponent, OrganGotRemovedEvent>(OnRemoved);
    }

    private void OnInserted(EntityUid uid, CyberneticEyesComponent comp, ref OrganGotInsertedEvent args)
    {
        if (!TryComp<UnpoweredFlashlightComponent>(uid, out var flashlight))
            return;

        // Body's organ container occludes light by default; opt out so the eye-cone reaches the world.
        // Subdermal implants do the same on their dedicated implant container.
        if (TryComp<BodyComponent>(args.Target, out var body))
            _body.SetOrgansOccludeLight((args.Target, body), false);

        _actions.AddAction(args.Target, ref flashlight.ToggleActionEntity, flashlight.ToggleAction, uid);
    }

    private void OnRemoved(EntityUid uid, CyberneticEyesComponent comp, ref OrganGotRemovedEvent args)
    {
        if (TerminatingOrDeleted(args.Target))
            return;

        if (!TryComp<UnpoweredFlashlightComponent>(uid, out var flashlight))
            return;

        _actions.RemoveAction(args.Target, flashlight.ToggleActionEntity);
        flashlight.ToggleActionEntity = null;

        // Make sure the light is off when the eyes leave the body.
        if (_light.TryGetLight(uid, out _))
            _light.SetEnabled(uid, false);

        flashlight.LightOn = false;
        Dirty(uid, flashlight);
    }
}
