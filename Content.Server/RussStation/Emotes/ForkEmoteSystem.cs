using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Body;
using Content.Shared.Chat;
using Content.Shared.RussStation.Emotes;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server.RussStation.Emotes;

/// HONK Fork-side dispatcher for new physical emotes (Fart, Flip, Spin). One subscription on
/// <see cref="BodyComponent"/> routes by emote id so the engine's
/// "no duplicate subscription" rule stays satisfied.
public sealed class ForkEmoteSystem : EntitySystem
{
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private const string FartEmoteId = "Fart";
    private const string FlipEmoteId = "Flip";
    private const string SpinEmoteId = "Spin";

    private const float MolesAmmoniaPerFart = 2.5f;
    private static readonly TimeSpan FlipDuration = TimeSpan.FromSeconds(0.5);
    private static readonly TimeSpan SpinDuration = TimeSpan.FromSeconds(2);

    private static readonly SoundSpecifier FartSound =
        new SoundCollectionSpecifier("Farts", AudioParams.Default.WithVariation(0.125f).WithVolume(-2f));

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BodyComponent, EmoteEvent>(OnEmote);
    }

    private void OnEmote(Entity<BodyComponent> ent, ref EmoteEvent args)
    {
        if (args.Handled)
            return;

        switch (args.Emote.ID)
        {
            case FartEmoteId:
                _audio.PlayPvs(FartSound, ent.Owner);
                _atmos.GetTileMixture(ent.Owner, excite: true)?.AdjustMoles(Gas.Ammonia, MolesAmmoniaPerFart);
                args.Handled = true;
                break;

            case FlipEmoteId:
                StartRotationAnim(ent.Owner, FlipDuration, 1f);
                args.Handled = true;
                break;

            case SpinEmoteId:
                StartRotationAnim(ent.Owner, SpinDuration, 2f);
                args.Handled = true;
                break;
        }
    }

    private void StartRotationAnim(EntityUid uid, TimeSpan duration, float turns)
    {
        // Replace any prior animation cleanly so back-to-back emotes restart the spin
        // instead of letting the old End fire mid-animation and yanking the component.
        var anim = EnsureComp<SpriteRotationAnimComponent>(uid);
        anim.Duration = duration;
        anim.Turns = turns;
        anim.EndTime = _timing.CurTime + duration;
        Dirty(uid, anim);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<SpriteRotationAnimComponent>();
        while (query.MoveNext(out var uid, out var anim))
        {
            if (anim.EndTime <= now)
                RemComp<SpriteRotationAnimComponent>(uid);
        }
    }
}
