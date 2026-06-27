using Content.Shared.RussStation.Light;

namespace Content.Server.RussStation.Light;

/// <summary>
///     Server-only cache of the recycler's stored-bulb summary shown in the UI. Kept off the
///     networked <see cref="LightReplacerRecyclerComponent"/> so the per-prototype count is rebuilt
///     only when the storage container changes, not on every state push. Added lazily and cleared
///     by the recycler system on container change.
/// </summary>
[RegisterComponent]
public sealed partial class LightReplacerRecyclerCacheComponent : Component
{
    /// <summary>
    ///     Cached per-prototype counts, or null when invalidated and awaiting a rebuild.
    /// </summary>
    public List<LightReplacerStoredBulb>? StoredBulbs;
}
