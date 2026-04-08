using Content.Shared.Damage;
using Content.Shared.Damage.Systems;

namespace Content.Shared.RussStation.Traits;

public sealed class ToughSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ToughComponent, DamageModifyEvent>(OnDamageModify);
    }

    private void OnDamageModify(EntityUid uid, ToughComponent component, DamageModifyEvent args)
    {
        args.Damage = args.Damage * component.DamageMultiplier;
    }
}
