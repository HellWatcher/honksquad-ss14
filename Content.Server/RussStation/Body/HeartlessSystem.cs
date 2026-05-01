using Content.Server.Body.Systems;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.RussStation.Body;
using Robust.Shared.Prototypes;

namespace Content.Server.RussStation.Body;

/// <summary>
/// Tracks heart presence in bodies and applies a fatal saturation drain when no heart is installed.
/// Uses NoHeartComponent as a persistent marker so a defibrillator cannot bypass it.
/// </summary>
public sealed class HeartlessSystem : EntitySystem
{
    [Dependency] private readonly RespiratorSystem _respirator = default!;

    private static readonly ProtoId<OrganCategoryPrototype> HeartCategory = "Heart";

    private const float DrainPerSecond = 3f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BodyComponent, OrganInsertedIntoEvent>(OnOrganInserted);
        SubscribeLocalEvent<BodyComponent, OrganRemovedFromEvent>(OnOrganRemoved);
    }

    private void OnOrganRemoved(Entity<BodyComponent> body, ref OrganRemovedFromEvent args)
    {
        if (!IsHeartCategory(args.Organ))
            return;

        if (!HasAnyHeart(body.Comp))
            EnsureComp<NoHeartComponent>(body);
    }

    private void OnOrganInserted(Entity<BodyComponent> body, ref OrganInsertedIntoEvent args)
    {
        if (!IsHeartCategory(args.Organ))
            return;

        RemComp<NoHeartComponent>(body);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var drain = -DrainPerSecond * frameTime;
        var query = EntityQueryEnumerator<NoHeartComponent>();

        while (query.MoveNext(out var uid, out _))
        {
            _respirator.UpdateSaturation(uid, drain);
        }
    }

    private bool IsHeartCategory(EntityUid organ)
    {
        return TryComp<OrganComponent>(organ, out var organComp)
               && organComp.Category == HeartCategory;
    }

    private bool HasAnyHeart(BodyComponent body)
    {
        if (body.Organs == null)
            return false;

        foreach (var organ in body.Organs.ContainedEntities)
        {
            if (IsHeartCategory(organ))
                return true;
        }

        return false;
    }
}
