using Content.Shared.Nutrition.EntitySystems;

namespace Content.Shared.RussStation.Traits;

public sealed class AgeusiaSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AgeusiaComponent, FlavorProfileModificationEvent>(OnFlavor);
    }

    private static void OnFlavor(EntityUid uid, AgeusiaComponent comp, FlavorProfileModificationEvent args)
    {
        if (args.User == uid)
            args.Flavors.Clear();
    }
}
