using Content.Shared.Gibbing;
//HONK START
using Content.Shared.RussStation.Body;
//HONK END

namespace Content.Shared.Body;

public sealed class GibbableOrganSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GibbableOrganComponent, BodyRelayedEvent<BeingGibbedEvent>>(OnBeingGibbed);
    }

    private void OnBeingGibbed(Entity<GibbableOrganComponent> ent, ref BodyRelayedEvent<BeingGibbedEvent> args)
    {
        //HONK START - Cybernetic organs inherit GibbableOrgan from base but shouldn't gib
        if (HasComp<CyberneticOrganComponent>(ent))
            return;
        //HONK END

        args.Args.Giblets.Add(ent);
    }
}
