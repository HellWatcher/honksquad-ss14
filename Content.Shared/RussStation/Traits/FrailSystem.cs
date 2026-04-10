using Content.Shared.Damage;
using Content.Shared.Damage.Systems;

namespace Content.Shared.RussStation.Traits;

public sealed class FrailSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FrailComponent, DamageModifyEvent>(OnDamageModify);
    }

    private void OnDamageModify(EntityUid uid, FrailComponent component, DamageModifyEvent args)
    {
        args.Damage = args.Damage * component.DamageMultiplier;
    }
}
