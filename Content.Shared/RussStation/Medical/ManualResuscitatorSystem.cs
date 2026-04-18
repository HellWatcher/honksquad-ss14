using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;

namespace Content.Shared.RussStation.Medical;

/// <summary>
/// Handles <see cref="ManualResuscitatorComponent"/>: starts a repeating
/// do-after that chips away at a critical patient's Asphyxiation damage until
/// they recover enough to breathe on their own, mirroring SS13 CPR.
/// </summary>
public sealed class ManualResuscitatorSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private static readonly Robust.Shared.Prototypes.ProtoId<Content.Shared.Damage.Prototypes.DamageTypePrototype>
        AsphyxiationType = "Asphyxiation";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ManualResuscitatorComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<ManualResuscitatorComponent, ManualResuscitatorDoAfterEvent>(OnDoAfter);
    }

    private void OnAfterInteract(Entity<ManualResuscitatorComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (!CanResuscitate(ent, target, args.User, popup: true))
            return;

        _audio.PlayPredicted(ent.Comp.SqueezeSound, ent.Owner, args.User);

        var started = _doAfter.TryStartDoAfter(new DoAfterArgs(
            EntityManager,
            args.User,
            ent.Comp.DoAfterDuration,
            new ManualResuscitatorDoAfterEvent(),
            ent.Owner,
            target,
            ent.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        });

        args.Handled = started;
    }

    private void OnDoAfter(Entity<ManualResuscitatorComponent> ent, ref ManualResuscitatorDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        if (args.Target is not { } target)
            return;

        if (!CanResuscitate(ent, target, args.User, popup: false))
        {
            args.Handled = true;
            return;
        }

        _damageable.TryChangeDamage(target, ent.Comp.Heal, true, origin: args.User);
        _audio.PlayPredicted(ent.Comp.SqueezeSound, ent.Owner, args.User);

        args.Repeat = CanResuscitate(ent, target, args.User, popup: false);
        args.Handled = true;
    }

    private bool CanResuscitate(Entity<ManualResuscitatorComponent> ent, EntityUid target, EntityUid user, bool popup)
    {
        if (target == user)
        {
            if (popup)
                _popup.PopupClient(Loc.GetString("manual-resuscitator-self"), ent.Owner, user);
            return false;
        }

        if (!TryComp<MobStateComponent>(target, out var mobState))
            return false;

        if (ent.Comp.CriticalOnly && !_mobState.IsCritical(target, mobState))
        {
            if (popup)
            {
                var msg = Loc.GetString("manual-resuscitator-not-critical",
                    ("target", Identity.Entity(target, EntityManager)));
                _popup.PopupClient(msg, target, user);
            }
            return false;
        }

        if (!HasComp<DamageableComponent>(target))
            return false;

#pragma warning disable CS0618 // GetAllDamage is obsolete but still the canonical read path used by HealingSystem/StethoscopeSystem.
        var damage = _damageable.GetAllDamage(target);
#pragma warning restore CS0618

        if (!damage.DamageDict.TryGetValue(AsphyxiationType, out var oxy)
            || oxy <= ent.Comp.StopThreshold)
        {
            if (popup)
            {
                var msg = Loc.GetString("manual-resuscitator-no-oxyloss",
                    ("target", Identity.Entity(target, EntityManager)));
                _popup.PopupClient(msg, target, user);
            }
            return false;
        }

        return true;
    }
}
