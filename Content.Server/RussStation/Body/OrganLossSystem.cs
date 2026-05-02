using Content.Server.Body.Systems;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.RussStation.Body;
using Content.Shared.RussStation.Hearing.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server.RussStation.Body;

/// <summary>
/// Tracks essential-organ presence in bodies and applies penalties when they are absent.
/// Adds <see cref="NoHeartComponent"/> when no heart is installed (drives a fatal saturation
/// drain each tick) and <see cref="NoEyesComponent"/> when no eyes are installed (drives full
/// blindness via <see cref="BlindableSystem"/>). Both markers persist so defibrillation /
/// temporary effects can't bypass the underlying organ loss.
/// </summary>
public sealed class OrganLossSystem : EntitySystem
{
    [Dependency] private readonly RespiratorSystem _respirator = default!;
    [Dependency] private readonly BlindableSystem _blinding = default!;
    [Dependency] private readonly DeafableSystem _deafable = default!;

    private static readonly ProtoId<OrganCategoryPrototype> HeartCategory = "Heart";
    private static readonly ProtoId<OrganCategoryPrototype> EyesCategory = "Eyes";
    private static readonly ProtoId<OrganCategoryPrototype> EarsCategory = "Ears";

    /// <summary>Saturation drained per second while the body has no heart.</summary>
    private const float NoHeartDrainPerSecond = 3f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BodyComponent, OrganInsertedIntoEvent>(OnOrganInserted);
        SubscribeLocalEvent<BodyComponent, OrganRemovedFromEvent>(OnOrganRemoved);

        SubscribeLocalEvent<NoEyesComponent, ComponentStartup>(OnNoEyesStartup);
        SubscribeLocalEvent<NoEyesComponent, ComponentShutdown>(OnNoEyesShutdown);
    }

    private void OnOrganRemoved(Entity<BodyComponent> body, ref OrganRemovedFromEvent args)
    {
        // Body cleanup removes organs as it terminates; don't ensure markers in that window.
        if (TerminatingOrDeleted(body.Owner))
            return;

        if (!TryComp<OrganComponent>(args.Organ, out var organ))
            return;

        if (organ.Category == HeartCategory && !HasAnyOrganOfCategory(body.Comp, HeartCategory))
            EnsureComp<NoHeartComponent>(body);
        else if (organ.Category == EyesCategory && !HasAnyOrganOfCategory(body.Comp, EyesCategory))
            EnsureComp<NoEyesComponent>(body);
        else if (organ.Category == EarsCategory)
            _deafable.UpdateIsDeaf(body.Owner);
    }

    private void OnOrganInserted(Entity<BodyComponent> body, ref OrganInsertedIntoEvent args)
    {
        if (!TryComp<OrganComponent>(args.Organ, out var organ))
            return;

        if (organ.Category == HeartCategory)
            RemComp<NoHeartComponent>(body);
        else if (organ.Category == EyesCategory)
            RemComp<NoEyesComponent>(body);
        else if (organ.Category == EarsCategory)
            _deafable.UpdateIsDeaf(body.Owner);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var drain = -NoHeartDrainPerSecond * frameTime;
        var query = EntityQueryEnumerator<NoHeartComponent>();

        while (query.MoveNext(out var uid, out _))
            _respirator.UpdateSaturation(uid, drain);
    }

    private void OnNoEyesStartup(Entity<NoEyesComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<BlindableComponent>(ent, out var blindable))
            return;

        // Use MaxDamage so the patient is fully blind (greyscale BlindOverlay), not just blurred.
        // Removing eyes is more severe than the PermanentBlindness trait.
        _blinding.SetMinDamage((ent.Owner, blindable), blindable.MaxDamage);
    }

    private void OnNoEyesShutdown(Entity<NoEyesComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp<BlindableComponent>(ent, out var blindable))
            return;

        if (blindable.MinDamage != 0)
            _blinding.SetMinDamage((ent.Owner, blindable), 0);

        _blinding.AdjustEyeDamage((ent.Owner, blindable), -blindable.EyeDamage);
    }

    private bool HasAnyOrganOfCategory(BodyComponent body, ProtoId<OrganCategoryPrototype> category)
    {
        if (body.Organs == null)
            return false;

        foreach (var organ in body.Organs.ContainedEntities)
        {
            if (TryComp<OrganComponent>(organ, out var organComp) && organComp.Category == category)
                return true;
        }

        return false;
    }
}
