using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Body;
using Content.Shared.Chat;
using Content.Shared.RussStation.Emotes;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;

namespace Content.Server.RussStation.Emotes;

/// HONK Fork-side dispatcher for new physical emotes (Fart, Flip, Spin). One subscription on
/// <see cref="BodyComponent"/> routes by emote id so the engine's
/// "no duplicate subscription" rule stays satisfied.
public sealed class ForkEmoteSystem : EntitySystem
{
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private const string FartEmoteId = "Fart";
    private const string FlipEmoteId = "Flip";
    private const string SpinEmoteId = "Spin";

    private const float MolesAmmoniaPerFart = 2.5f;
    private static readonly TimeSpan FlipDuration = TimeSpan.FromSeconds(0.5);
    private const float FlipTurns = 1f;
    private static readonly TimeSpan SpinDuration = TimeSpan.FromSeconds(0.5);
    /// One full S→E→N→W cycle in SpinDuration. Bump if you want it spinnier.
    private const float SpinSteps = 4f;

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
                BroadcastSpriteEmote(ent.Owner, SpriteEmoteKind.Flip, FlipDuration, FlipTurns);
                args.Handled = true;
                break;

            case SpinEmoteId:
                BroadcastSpriteEmote(ent.Owner, SpriteEmoteKind.Spin, SpinDuration, SpinSteps);
                args.Handled = true;
                break;
        }
    }

    private void BroadcastSpriteEmote(EntityUid uid, SpriteEmoteKind kind, TimeSpan duration, float amount)
    {
        var ev = new SpriteEmoteAnimEvent(GetNetEntity(uid), kind, duration, amount);
        // PVS-scoped: only sessions that can see the mob get the animation.
        RaiseNetworkEvent(ev, Filter.Pvs(uid));
    }
}
