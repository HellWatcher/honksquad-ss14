using Content.Shared.Actions;
using Content.Shared.Body;
using Robust.Shared.Prototypes;

namespace Content.Shared.RussStation.Skillchips.Systems;

public abstract class SharedSkillchipSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SkillchipHolderComponent, OrganGotInsertedEvent>(OnBrainInserted);
        SubscribeLocalEvent<SkillchipHolderComponent, OrganGotRemovedEvent>(OnBrainRemoved);
    }

    // ── Brain lifecycle ──────────────────────────────────────────────────────

    private void OnBrainInserted(Entity<SkillchipHolderComponent> brain, ref OrganGotInsertedEvent args)
    {
        ApplyAllGrants(brain, args.Target);
    }

    private void OnBrainRemoved(Entity<SkillchipHolderComponent> brain, ref OrganGotRemovedEvent args)
    {
        if (LifeStage(args.Target) >= EntityLifeStage.Terminating)
            return;

        RevertAllGrants(brain, args.Target);
    }

    // ── Install / Remove ─────────────────────────────────────────────────────

    /// <summary>
    /// Installs a chip into the brain. Returns false if over capacity or already installed.
    /// </summary>
    public bool TryInstall(Entity<SkillchipHolderComponent> brain, ProtoId<SkillchipPrototype> chipProto)
    {
        if (brain.Comp.ImplantedChips.Contains(chipProto))
            return false;

        var prototype = _proto.Index(chipProto);
        if (UsedCapacity(brain.Comp) + prototype.CapacityCost > brain.Comp.MaxCapacity)
            return false;

        if (prototype.Category != null && HasCategoryInstalled(brain.Comp, prototype.Category))
            return false;

        // Apply grants first so a thrown grant leaves the holder untouched. Recording the chip
        // last keeps capacity accounting consistent with what is actually active on the mob.
        if (GetBody(brain) is { } body)
            ApplyChipGrants(brain, body, prototype);

        brain.Comp.ImplantedChips.Add(chipProto);
        Dirty(brain);

        return true;
    }

    /// <summary>
    /// Removes a chip from the brain. Returns false if the chip is not installed.
    /// </summary>
    public bool TryRemove(Entity<SkillchipHolderComponent> brain, ProtoId<SkillchipPrototype> chipProto)
    {
        if (!brain.Comp.ImplantedChips.Remove(chipProto))
            return false;

        Dirty(brain);

        if (GetBody(brain) is { } body)
            RevertChipGrants(brain, body, _proto.Index(chipProto));

        return true;
    }

    // ── Capability query ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns true if the brain entity has the given capability tag active.
    /// </summary>
    public bool BrainHasCapability(EntityUid brain, string tag)
    {
        return TryComp<SkillchipHolderComponent>(brain, out var holder)
               && holder.CapabilityTags.Contains(tag);
    }

    /// <summary>
    /// Returns true if any SkillchipHolderComponent child of the mob has the given capability tag.
    /// </summary>
    public bool HasCapability(EntityUid mob, string tag)
    {
        var enumerator = Transform(mob).ChildEnumerator;
        while (enumerator.MoveNext(out var child))
        {
            if (BrainHasCapability(child, tag))
                return true;
        }

        return false;
    }

    // ── Helpers for external systems ──────────────────────────────────────────

    /// <summary>
    /// Finds the first SkillchipHolderComponent child of the mob and returns it.
    /// Returns false if the mob has no implanted brain.
    /// </summary>
    public bool TryGetBrain(EntityUid mob, out Entity<SkillchipHolderComponent> brain)
    {
        var enumerator = Transform(mob).ChildEnumerator;
        while (enumerator.MoveNext(out var child))
        {
            if (TryComp<SkillchipHolderComponent>(child, out var holder))
            {
                brain = (child, holder);
                return true;
            }
        }

        brain = default;
        return false;
    }

    /// <summary>
    /// Returns true if the given chip proto is installed in the mob's brain.
    /// </summary>
    public bool HasChipInstalled(EntityUid mob, ProtoId<SkillchipPrototype> chipProto)
    {
        if (!TryGetBrain(mob, out var brain))
            return false;
        return brain.Comp.ImplantedChips.Contains(chipProto);
    }

    /// <summary>
    /// Returns a snapshot of the installed chip proto IDs for a mob.
    /// </summary>
    public IReadOnlyList<ProtoId<SkillchipPrototype>> GetInstalledChips(EntityUid mob)
    {
        if (!TryGetBrain(mob, out var brain))
            return Array.Empty<ProtoId<SkillchipPrototype>>();
        return brain.Comp.ImplantedChips;
    }

    /// <summary>
    /// Returns current used and max capacity for a mob's brain.
    /// </summary>
    public (int used, int max) GetCapacity(EntityUid mob)
    {
        if (!TryGetBrain(mob, out var brain))
            return (0, 0);
        return (UsedCapacity(brain.Comp), brain.Comp.MaxCapacity);
    }

    public void EnsureMobComponent<T>(EntityUid mob) where T : Component, new()
        => EnsureComp<T>(mob);

    public void RemoveMobComponent<T>(EntityUid mob) where T : Component
        => RemComp<T>(mob);

    // ── Grant application helpers (called by SkillchipGrant subclasses) ──────

    public void ApplyActionGrant(EntityUid mob, EntityUid brain, EntProtoId actionProto)
    {
        if (!TryComp<SkillchipHolderComponent>(brain, out var holder))
            return;

        var key = $"{brain}:{actionProto}";
        if (holder.ActionEntities.ContainsKey(key))
            return;

        EntityUid? actionEnt = null;
        _actions.AddAction(mob, ref actionEnt, actionProto);

        if (actionEnt != null)
            holder.ActionEntities[key] = actionEnt.Value;
    }

    public void RevertActionGrant(EntityUid mob, EntityUid brain, EntProtoId actionProto)
    {
        if (!TryComp<SkillchipHolderComponent>(brain, out var holder))
            return;

        var key = $"{brain}:{actionProto}";
        if (!holder.ActionEntities.TryGetValue(key, out var actionEnt))
            return;

        _actions.RemoveAction(mob, actionEnt);
        holder.ActionEntities.Remove(key);
    }

    public void ApplyCapabilityTag(EntityUid brain, string tag)
    {
        if (!TryComp<SkillchipHolderComponent>(brain, out var holder))
            return;

        holder.CapabilityTags.Add(tag);
        Dirty(brain, holder);
    }

    public void RevertCapabilityTag(EntityUid brain, string tag)
    {
        if (!TryComp<SkillchipHolderComponent>(brain, out var holder))
            return;

        holder.CapabilityTags.Remove(tag);
        Dirty(brain, holder);
    }

    // ── Internals ────────────────────────────────────────────────────────────

    protected void ApplyAllGrants(Entity<SkillchipHolderComponent> brain, EntityUid mob)
    {
        foreach (var chipProto in brain.Comp.ImplantedChips)
            ApplyChipGrants(brain, mob, _proto.Index(chipProto));
    }

    protected void RevertAllGrants(Entity<SkillchipHolderComponent> brain, EntityUid mob)
    {
        foreach (var chipProto in brain.Comp.ImplantedChips)
            RevertChipGrants(brain, mob, _proto.Index(chipProto));
    }

    private void ApplyChipGrants(Entity<SkillchipHolderComponent> brain, EntityUid mob, SkillchipPrototype prototype)
    {
        foreach (var grant in prototype.Grants)
            grant.Apply(mob, brain, this);
    }

    private void RevertChipGrants(Entity<SkillchipHolderComponent> brain, EntityUid mob, SkillchipPrototype prototype)
    {
        foreach (var grant in prototype.Grants)
            grant.Revert(mob, brain, this);
    }

    private int UsedCapacity(SkillchipHolderComponent holder)
    {
        var total = 0;
        foreach (var chipProto in holder.ImplantedChips)
            total += _proto.Index(chipProto).CapacityCost;
        return total;
    }

    private bool HasCategoryInstalled(SkillchipHolderComponent holder, string category)
    {
        foreach (var installed in holder.ImplantedChips)
        {
            if (_proto.Index(installed).Category == category)
                return true;
        }

        return false;
    }

    private EntityUid? GetBody(Entity<SkillchipHolderComponent> brain)
    {
        return TryComp<OrganComponent>(brain, out var organ) ? organ.Body : null;
    }
}
