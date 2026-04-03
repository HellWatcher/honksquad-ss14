using Content.Shared.RussStation.Memories;

namespace Content.Server.RussStation.Memories;

public sealed class MemoriesSystem : EntitySystem
{
    /// <summary>
    /// Add a memory to the entity. Creates the component if it doesn't exist.
    /// </summary>
    public void AddMemory(EntityUid uid, string key, string value, MemoriesComponent? comp = null)
    {
        comp ??= EnsureComp<MemoriesComponent>(uid);
        comp.Memories[key] = value;
        Dirty(uid, comp);
    }

    /// <summary>
    /// Remove a memory by key. Returns false if the key didn't exist.
    /// </summary>
    public bool RemoveMemory(EntityUid uid, string key, MemoriesComponent? comp = null)
    {
        if (!Resolve(uid, ref comp, false))
            return false;

        if (!comp.Memories.Remove(key))
            return false;

        Dirty(uid, comp);
        return true;
    }
}
