using Content.Shared.Body;
using Content.Shared.Emp;
using Content.Shared.RussStation.Skillchips;
using Content.Shared.RussStation.Skillchips.Systems;

namespace Content.Server.RussStation.Skillchips;

public sealed class SkillchipSystem : SharedSkillchipSystem
{
    public override void Initialize()
    {
        base.Initialize();

        // EmpDisabledComponent is added to the mob when hit by an EMP, so the disable side can
        // filter on it (ComponentInit fires before the component is fully attached, but the
        // entity uid is available). For the remove side, the directed EmpDisabledRemovedEvent
        // fires after the engine has torn the component back off, so we filter on BodyComponent
        // (always present on the mob) to receive the directed dispatch.
        SubscribeLocalEvent<EmpDisabledComponent, ComponentInit>(OnEmpInit);
        SubscribeLocalEvent<BodyComponent, EmpDisabledRemovedEvent>(OnEmpRemove);
    }

    private void OnEmpInit(Entity<EmpDisabledComponent> mob, ref ComponentInit args)
    {
        ForEachImplantedBrain(mob, (brain, holder) => RevertAllGrants((brain, holder), mob));
    }

    private void OnEmpRemove(Entity<BodyComponent> mob, ref EmpDisabledRemovedEvent args)
    {
        if (LifeStage(mob) >= EntityLifeStage.Terminating)
            return;

        ForEachImplantedBrain(mob, (brain, holder) => ApplyAllGrants((brain, holder), mob));
    }

    private void ForEachImplantedBrain(EntityUid mob, Action<EntityUid, SkillchipHolderComponent> action)
    {
        var enumerator = Transform(mob).ChildEnumerator;
        while (enumerator.MoveNext(out var child))
        {
            if (TryComp<SkillchipHolderComponent>(child, out var holder))
                action(child, holder);
        }
    }
}
