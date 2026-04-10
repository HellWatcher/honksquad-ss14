using Content.Shared.Chat;

namespace Content.Shared.RussStation.Traits;

public sealed class BoomingVoiceSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BoomingVoiceComponent, GetVoiceRangeEvent>(OnGetVoiceRange);
    }

    private void OnGetVoiceRange(EntityUid uid, BoomingVoiceComponent component, ref GetVoiceRangeEvent args)
    {
        args.Range *= component.RangeMultiplier;
    }
}
