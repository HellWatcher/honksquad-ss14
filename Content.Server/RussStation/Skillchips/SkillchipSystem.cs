using Content.Shared.Emp;
using Content.Shared.RussStation.Skillchips;
using Content.Shared.RussStation.Skillchips.Systems;

namespace Content.Server.RussStation.Skillchips;

public sealed class SkillchipSystem : SharedSkillchipSystem
{
    public override void Initialize()
    {
        base.Initialize();

        // EmpDisabledComponent is added to the mob entity when hit by an EMP pulse.
        // Walk the mob's direct children to find any implanted brain and revert/restore grants.
        SubscribeLocalEvent<EmpDisabledComponent, ComponentInit>(OnEmpInit);
        SubscribeLocalEvent<EmpDisabledComponent, ComponentRemove>(OnEmpRemove);
    }

    private void OnEmpInit(Entity<EmpDisabledComponent> mob, ref ComponentInit args)
    {
        ForEachImplantedBrain(mob, (brain, holder) => RevertAllGrants((brain, holder), mob));
    }

    private void OnEmpRemove(Entity<EmpDisabledComponent> mob, ref ComponentRemove args)
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
