using Content.Server.Body.Systems;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.RussStation.Body;
using Robust.Shared.Prototypes;

namespace Content.Server.RussStation.Body;

/// <summary>
/// Tracks heart presence on bodies. While a body has no Heart-category organ installed,
/// <see cref="NoHeartComponent"/> is present and the body bleeds saturation each tick so
/// the patient suffocates fatally. Defibrillation does not clear this -- a heart has to
/// physically be reinserted.
/// </summary>
public sealed class HeartlessSystem : EntitySystem
{
    [Dependency] private readonly RespiratorSystem _respirator = default!;

    private static readonly ProtoId<OrganCategoryPrototype> HeartCategory = "Heart";

    /// <summary>Saturation drained per second while the body has no heart.</summary>
    private const float HeartlessDrainPerSecond = 3f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BodyComponent, OrganInsertedIntoEvent>(OnOrganInserted);
        SubscribeLocalEvent<BodyComponent, OrganRemovedFromEvent>(OnOrganRemoved);
    }

    private void OnOrganRemoved(Entity<BodyComponent> body, ref OrganRemovedFromEvent args)
    {
        // Body cleanup removes organs as it terminates; don't ensure markers in that window.
        if (TerminatingOrDeleted(body.Owner))
            return;

        if (!TryComp<OrganComponent>(args.Organ, out var organ))
            return;

        if (organ.Category == HeartCategory && !HasAnyHeart(body.Comp))
            EnsureComp<NoHeartComponent>(body);
    }

    private void OnOrganInserted(Entity<BodyComponent> body, ref OrganInsertedIntoEvent args)
    {
        if (!TryComp<OrganComponent>(args.Organ, out var organ))
            return;

        if (organ.Category == HeartCategory)
            RemComp<NoHeartComponent>(body);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var drain = -HeartlessDrainPerSecond * frameTime;
        var query = EntityQueryEnumerator<NoHeartComponent>();

        while (query.MoveNext(out var uid, out _))
            _respirator.UpdateSaturation(uid, drain);
    }

    private bool HasAnyHeart(BodyComponent body)
    {
        if (body.Organs == null)
            return false;

        foreach (var organ in body.Organs.ContainedEntities)
        {
            if (TryComp<OrganComponent>(organ, out var organComp) && organComp.Category == HeartCategory)
                return true;
        }

        return false;
    }
}
