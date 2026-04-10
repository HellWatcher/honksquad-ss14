using Content.Shared.Chat;

namespace Content.Shared.RussStation.Traits;

public sealed class SoftSpokenSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SoftSpokenComponent, GetVoiceRangeEvent>(OnGetVoiceRange);
    }

    private void OnGetVoiceRange(EntityUid uid, SoftSpokenComponent component, ref GetVoiceRangeEvent args)
    {
        args.Range *= component.RangeMultiplier;
    }
}
