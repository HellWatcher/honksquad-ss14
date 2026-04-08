using Content.Shared.Examine;

namespace Content.Shared.RussStation.Traits;

public sealed class EagleEyeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<EagleEyeComponent, GetExamineRangeEvent>(OnGetExamineRange);
    }

    private void OnGetExamineRange(EntityUid uid, EagleEyeComponent component, ref GetExamineRangeEvent args)
    {
        args.Range = component.ExamineRange;
    }
}
