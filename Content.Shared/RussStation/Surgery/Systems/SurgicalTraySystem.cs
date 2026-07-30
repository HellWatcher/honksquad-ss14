using Content.Shared.Foldable;
using Content.Shared.Friction;
using Content.Shared.RussStation.Surgery.Components;

namespace Content.Shared.RussStation.Surgery.Systems;

public sealed partial class SurgicalTraySystem : EntitySystem
{
    [Dependency] private TileFrictionController _friction = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SurgicalTrayComponent, FoldedEvent>(OnFolded);
    }

    private void OnFolded(EntityUid uid, SurgicalTrayComponent component, ref FoldedEvent args)
    {
        var value = args.IsFolded ? component.FoldedFriction : component.UnfoldedFriction;
        _friction.SetModifier(uid, value);
    }
}
