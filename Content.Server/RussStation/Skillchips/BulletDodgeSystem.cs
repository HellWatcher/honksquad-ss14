using Content.Shared.Chat;
using Content.Shared.Damage.Systems;
using Content.Shared.Projectiles;
using Content.Shared.RussStation.Skillchips;
using Robust.Shared.Timing;

namespace Content.Server.RussStation.Skillchips;

/// <summary>
/// Opens a brief projectile-deflect window when the mob performs the trigger emote.
/// Consumes stamina on each successful deflect and closes the window immediately after.
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
        if (args.Emote.ID != ent.Comp.ActivateEmoteId)
            return;

        ent.Comp.ActiveUntil = _timing.CurTime + ent.Comp.DodgeWindow;
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
}
