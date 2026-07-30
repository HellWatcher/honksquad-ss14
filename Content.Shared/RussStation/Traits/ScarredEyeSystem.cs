using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.RussStation.Eye;

namespace Content.Shared.RussStation.Traits;

public sealed partial class ScarredEyeSystem : EntitySystem
{
    [Dependency] private BlindableSystem _blinding = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ScarredEyeComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ScarredEyeComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnMapInit(Entity<ScarredEyeComponent> ent, ref MapInitEvent args)
        => BlindnessSourceHelper.Apply(EntityManager, _blinding, ent, ent.Comp.Blindness);

    private void OnShutdown(Entity<ScarredEyeComponent> ent, ref ComponentShutdown args)
        => BlindnessSourceHelper.Remove(EntityManager, _blinding, ent);
}
