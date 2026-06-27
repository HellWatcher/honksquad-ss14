using Content.Shared.Body;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Shared.RussStation.Body;

/// <summary>
/// Shared organ-slot lookup and replacement so the find-by-category rule lives in one place
/// instead of being copy-pasted between the autosurgeon and manual surgery. Categorized organs
/// occupy a single slot per <see cref="OrganCategoryPrototype"/> (one heart, one set of lungs, ...).
/// </summary>
public static class OrganReplacementHelper
{
    /// <summary>
    /// Find the first organ in <paramref name="organs"/> whose <see cref="OrganComponent.Category"/>
    /// matches <paramref name="category"/>. Uncategorized organs (null category) never match, mirroring
    /// the callers that only deduplicate categorized slots by category.
    /// </summary>
    public static bool TryFindOrganByCategory(
        IEntityManager entMan,
        BaseContainer organs,
        ProtoId<OrganCategoryPrototype>? category,
        out EntityUid found)
    {
        found = default;

        if (category == null)
            return false;

        foreach (var organ in organs.ContainedEntities)
        {
            if (entMan.TryGetComponent<OrganComponent>(organ, out var organComp) &&
                organComp.Category == category)
            {
                found = organ;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Install <paramref name="newOrgan"/> into <paramref name="organs"/>, first removing any existing
    /// organ that occupies the same category slot and dropping it next to <paramref name="body"/>.
    /// Uncategorized organs are simply inserted without displacing anything.
    /// </summary>
    public static void ReplaceOrganByCategory(
        IEntityManager entMan,
        SharedContainerSystem container,
        SharedTransformSystem transform,
        EntityUid body,
        BaseContainer organs,
        EntityUid newOrgan,
        ProtoId<OrganCategoryPrototype>? category)
    {
        if (TryFindOrganByCategory(entMan, organs, category, out var existing))
        {
            container.Remove(existing, organs);
            transform.DropNextTo(existing, body);
        }

        container.Insert(newOrgan, organs, force: true);
    }
}
