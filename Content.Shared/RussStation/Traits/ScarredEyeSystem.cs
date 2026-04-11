using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;

namespace Content.Shared.RussStation.Traits;

public sealed class ScarredEyeSystem : EntitySystem
{
    [Dependency] private readonly BlindableSystem _blinding = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ScarredEyeComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ScarredEyeComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnMapInit(Entity<ScarredEyeComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<BlindableComponent>(ent, out var blindable))
            return;

        _blinding.SetMinDamage((ent, blindable), ent.Comp.Blindness);
    }

    // Mirrors PermanentBlindnessSystem.OnShutdown so changelings and surgical cures
    // leave the eye in a fully-healed state instead of stuck blind minus the marker.
    private void OnShutdown(Entity<ScarredEyeComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp<BlindableComponent>(ent, out var blindable))
            return;

        if (blindable.MinDamage != 0)
            _blinding.SetMinDamage((ent, blindable), 0);

        _blinding.AdjustEyeDamage((ent, blindable), -blindable.EyeDamage);
    }
}
