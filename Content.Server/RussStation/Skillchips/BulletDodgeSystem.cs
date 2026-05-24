using Content.Shared.Chat;
using Content.Shared.Damage.Systems;
using Content.Shared.Projectiles;
using Content.Shared.RussStation.Emotes;
using Content.Shared.RussStation.Skillchips;
using Robust.Shared.Timing;

namespace Content.Server.RussStation.Skillchips;

/// <summary>
/// Opens a brief projectile-deflect window when the mob performs a trigger emote.
/// The window length matches the firing emote's animation duration, so a Flip
/// covers exactly as long as the flip animation is visible. Consumes stamina on
/// each successful deflect and closes the window immediately after.
/// </summary>
public sealed class BulletDodgeSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BulletDodgeComponent, EmoteEvent>(OnEmote);
        SubscribeLocalEvent<BulletDodgeComponent, ProjectileReflectAttemptEvent>(OnReflectAttempt);
    }

    private void OnEmote(Entity<BulletDodgeComponent> ent, ref EmoteEvent args)
    {
        if (!ent.Comp.ActivateEmoteIds.Contains(args.Emote.ID))
            return;

        if (GetWindowFor(args.Emote.ID) is not { } window)
            return;

        ent.Comp.ActiveUntil = _timing.CurTime + window;
    }

    private void OnReflectAttempt(Entity<BulletDodgeComponent> ent, ref ProjectileReflectAttemptEvent args)
    {
        if (ent.Comp.ActiveUntil == null || _timing.CurTime > ent.Comp.ActiveUntil)
        {
            ent.Comp.ActiveUntil = null;
            return;
        }

        args.Cancelled = true;
        ent.Comp.ActiveUntil = null;
        _stamina.TakeStaminaDamage(ent, ent.Comp.StaminaCost);
    }

    private static TimeSpan? GetWindowFor(string emoteId) => emoteId switch
    {
        "Flip" => ForkEmoteDurations.Flip,
        "Spin" => ForkEmoteDurations.Spin,
        _ => null,
    };
}
