using Content.Shared.Examine;

namespace Content.Shared.RussStation.Traits;

public sealed class TouchySystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TouchyComponent, GetExamineRangeEvent>(OnGetExamineRange);
    }

    private void OnGetExamineRange(EntityUid uid, TouchyComponent component, ref GetExamineRangeEvent args)
    {
        args.Range = component.ExamineRange;
    }
}
