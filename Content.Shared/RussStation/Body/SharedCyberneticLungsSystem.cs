using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Metabolism;
using Robust.Shared.Prototypes;

namespace Content.Shared.RussStation.Body;

/// <summary>
/// Copies the host body's metabolizer types onto cybernetic lung entities at insertion time,
/// so the Oxygenate MetabolizerTypeCondition resolves correctly for any species.
/// </summary>
public sealed class SharedCyberneticLungsSystem : EntitySystem
{
    private EntityQuery<MetabolizerComponent> _metabolizerQuery;

    public override void Initialize()
    {
        base.Initialize();

        _metabolizerQuery = GetEntityQuery<MetabolizerComponent>();

        SubscribeLocalEvent<LungComponent, OrganGotInsertedEvent>(OnLungInserted);
    }

    private void OnLungInserted(EntityUid uid, LungComponent _, ref OrganGotInsertedEvent args)
    {
        if (!HasComp<CyberneticOrganComponent>(uid))
            return;

        if (!_metabolizerQuery.TryComp(uid, out var metabolizer))
            return;

        if (!TryComp<BodyComponent>(args.Target, out var body) || body.Organs == null)
            return;

        var types = new HashSet<ProtoId<MetabolizerTypePrototype>>();
        foreach (var organ in body.Organs.ContainedEntities)
        {
            if (!_metabolizerQuery.TryComp(organ, out var otherMet) || otherMet.MetabolizerTypes == null)
                continue;

            types.UnionWith(otherMet.MetabolizerTypes);
        }

        if (types.Count == 0)
            return;

        metabolizer.MetabolizerTypes = types;
        Dirty(uid, metabolizer);
    }
}
