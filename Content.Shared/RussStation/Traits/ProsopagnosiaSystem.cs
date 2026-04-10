using Content.Shared.Humanoid;
using Robust.Shared.Enums;

namespace Content.Shared.RussStation.Traits;

public sealed class ProsopagnosiaSystem : EntitySystem
{
    [Dependency] private readonly HumanoidProfileSystem _profile = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ProsopagnosiaComponent, GetIdentityNameEvent>(OnGetIdentityName);
    }

    private void OnGetIdentityName(EntityUid uid, ProsopagnosiaComponent component,
        ref GetIdentityNameEvent args)
    {
        if (args.Handled)
            return;

        if (args.Target == uid)
            return;

        if (!TryComp<HumanoidProfileComponent>(args.Target, out var profile))
            return;

        var ageString = _profile.GetAgeRepresentation(profile.Species, profile.Age);
        var genderString = profile.Gender switch
        {
            Gender.Female => Loc.GetString("identity-gender-feminine"),
            Gender.Male => Loc.GetString("identity-gender-masculine"),
            _ => Loc.GetString("identity-gender-person"),
        };

        args.Name = $"{ageString} {genderString}";
        args.Handled = true;
    }
}
