namespace Content.Shared.RussStation.Shared;

/// <summary>
/// Base class for systems that manage a pair of marker components whose shutdown
/// handlers remove each other symmetrically (e.g. ActiveCarrier &lt;-&gt; BeingCarried).
/// Symmetric removal is recursion-prone: without a guard, each shutdown handler
/// re-enters the other and eventually calls RemComp on an already-Stopping component,
/// which trips LifeShutdown's debug assert. The two helpers below encode the guard
/// as the only supported way to perform the removal or respond to a structural
/// event during teardown, so the recursion class is unreachable by construction.
///
/// See #502 and #511 for the two bugs this class exists to prevent from recurring.
/// </summary>
public abstract class PairedMarkerSystem : EntitySystem
{
    /// <summary>
    /// Remove a paired marker from <paramref name="target"/> if and only if the
    /// removal is safe: the entity isn't terminating and the component isn't
    /// already in its own shutdown. Returns true if a removal was performed.
    /// </summary>
    protected bool TryRemovePaired<T>(EntityUid target) where T : IComponent
    {
        if (Terminating(target))
            return false;
        if (!TryComp<T>(target, out var comp) || comp.LifeStage >= ComponentLifeStage.Stopping)
            return false;
        RemComp<T>(target);
        return true;
    }

    /// <summary>
    /// True if the component is at or past <see cref="ComponentLifeStage.Stopping"/>.
    /// Call at the top of reactive handlers (EntParentChangedMessage, container
    /// events, etc.) whose own component may re-enter teardown mid-shutdown.
    /// </summary>
    protected static bool IsShuttingDown(IComponent component)
        => component.LifeStage >= ComponentLifeStage.Stopping;
}
