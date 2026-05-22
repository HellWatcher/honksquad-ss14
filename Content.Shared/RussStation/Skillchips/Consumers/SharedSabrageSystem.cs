using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.RussStation.Skillchips.Systems;
using Content.Shared.Weapons.Melee;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Fluids;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Random;

namespace Content.Shared.RussStation.Skillchips.Consumers;

/// <summary>
/// Interaction handler for the Le S48R4G3 skillchip (<c>sabrage_proficiency</c>
/// capability). Striking a sealed bottle tagged with
/// <see cref="SabrageableComponent"/> using a sharp melee item rolls for a
/// clean cork pop; failure smashes the bottle on the user and spills the
/// solution. Lives in shared so the client can predict the swing and the
/// success path. SS13 parallel:
/// <c>/obj/item/reagent_containers/cup/glass/bottle/champagne</c>'s
/// <c>attackby</c> branch and its 2-second do-after.
/// </summary>
public sealed class SharedSabrageSystem : EntitySystem
{
    public const string SabrageTag = "sabrage_proficiency";

    [Dependency] private readonly SharedSkillchipSystem _skillchip = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly OpenableSystem _openable = default!;
    [Dependency] private readonly SealableSystem _sealable = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private readonly SharedPuddleSystem _puddle = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SabrageableComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<SabrageableComponent, SabrageDoAfterEvent>(OnSwingFinished);
    }

    private void OnInteractUsing(Entity<SabrageableComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        // Only sealed bottles can be sabraged; once popped there is no cork to slice.
        if (!_sealable.IsSealed(ent.Owner))
            return;

        // Sharp + at-or-above the minimum effective melee damage. SS13 reads this
        // off item.force; the SS14 equivalent is the melee component's Damage spec.
        if (!TryComp<MeleeWeaponComponent>(args.Used, out var melee))
            return;

        var damage = (float) melee.Damage.GetTotal();
        if (damage < ent.Comp.MinimumDamage)
            return;

        // Chip-as-bonus: anyone with a sharp can try; the chip just dramatically
        // improves the odds. The capability check moves to the resolution step.
        args.Handled = true;

        _audio.PlayPredicted(new SoundPathSpecifier(ent.Comp.SwingSound), ent.Owner, args.User);
        _popup.PopupClient(Loc.GetString("skillchip-sabrage-start"), ent.Owner, args.User);

        var doAfter = new DoAfterArgs(
            EntityManager,
            args.User,
            ent.Comp.SwingDuration,
            new SabrageDoAfterEvent(),
            ent.Owner,
            target: ent.Owner,
            used: args.Used)
        {
            BreakOnMove = true,
            NeedHand = true,
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnSwingFinished(Entity<SabrageableComponent> ent, ref SabrageDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.User == default)
            return;

        // Random rolls and entity deletion only run on the server; client would
        // pick a different roll and pop a misleading message.
        if (!_net.IsServer)
        {
            args.Handled = true;
            return;
        }

        if (!TryComp<MeleeWeaponComponent>(args.Used, out var melee))
            return;

        var damage = (float) melee.Damage.GetTotal();

        // Base curve: 20·sqrt(d) - 33, clamped to >= 0. Hits ~12% at d=5,
        // ~30% at a knife, ~50% at a saber. The chip doubles whatever the
        // base lands on, clamped to 100.
        var baseChance = Math.Max(0f, ent.Comp.SuccessSqrtFactor * MathF.Sqrt(damage) + ent.Comp.BaseOffset);
        if (_skillchip.HasCapability(args.User, SabrageTag))
            baseChance *= ent.Comp.SkillchipMultiplier;
        var chance = Math.Clamp(baseChance, 0f, 100f);

        if (_random.NextFloat() * 100f < chance)
            Succeed(ent, args.User);
        else
            Fail(ent, args.User);

        args.Handled = true;
    }

    private void Succeed(Entity<SabrageableComponent> ent, EntityUid user)
    {
        // TryOpen fires OpenableOpenedEvent, which SealableSystem catches to
        // flip Sealed = false. The cork pop sound is the openable sound; the
        // sabrage success sound layers on top.
        _openable.TryOpen(ent.Owner, user: user);
        _audio.PlayPvs(new SoundPathSpecifier(ent.Comp.SuccessSound), ent.Owner);
        _popup.PopupEntity(Loc.GetString("skillchip-sabrage-success", ("user", user), ("bottle", ent.Owner)), ent.Owner);
    }

    private void Fail(Entity<SabrageableComponent> ent, EntityUid user)
    {
        _damageable.TryChangeDamage(user, ent.Comp.FailureDamage, origin: user);
        _audio.PlayPvs(new SoundPathSpecifier(ent.Comp.FailureSound), ent.Owner);
        _popup.PopupEntity(Loc.GetString("skillchip-sabrage-fail", ("user", user), ("bottle", ent.Owner)), ent.Owner);

        // Spill whatever was in the bottle onto the user's tile, then shatter
        // the bottle into shards.
        var coords = Transform(user).Coordinates;
        if (_solutionContainer.TryGetDrainableSolution(ent.Owner, out var solutionEnt, out var solution))
        {
            var drained = _solutionContainer.SplitSolution(solutionEnt.Value, solution.Volume);
            _puddle.TrySpillAt(coords, drained, out _);
        }

        Spawn(ent.Comp.BrokenPrototype, coords);
        Del(ent.Owner);
    }
}
