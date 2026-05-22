using Content.Shared.Examine;
using Content.Shared.RussStation.Skillchips.Systems;

namespace Content.Shared.RussStation.Skillchips.Consumers;

/// <summary>
/// Examine hook for the GENUINE ID Appraisal Now! skillchip
/// (<c>id_appraisal</c> capability). When a chip holder examines a tagged
/// <see cref="IdAppraisableComponent"/> ID card inside details range, append
/// a chip-only line stating whether the card came out of CentCom or a
/// station ID console. CentCom cards are flagged with
/// <see cref="CentcomIssuedComponent"/> directly on the prototype; the
/// <see cref="Content.Shared.Access.Components.IdCardComponent.JobPrototype"/>
/// field can't be used here because the preset card system never populates
/// it (only the ID console does). Lives in shared so the client sees the
/// line on its predicted examine.
/// </summary>
public sealed class SharedIdAppraiserSystem : EntitySystem
{
    public const string IdAppraisalTag = "id_appraisal";

    [Dependency] private readonly SharedSkillchipSystem _skillchip = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<IdAppraisableComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<IdAppraisableComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (!_skillchip.HasCapability(args.Examiner, IdAppraisalTag))
            return;

        string loc;
        if (HasComp<CentcomIssuedComponent>(ent))
            loc = "skillchip-id-appraisal-examine-centcom";
        else if (HasComp<SyndicateIssuedComponent>(ent))
            loc = "skillchip-id-appraisal-examine-syndicate";
        else
            loc = "skillchip-id-appraisal-examine-station";

        args.PushMarkup(Loc.GetString(loc));
    }
}
