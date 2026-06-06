using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Shared.RussStation.Scanner;

/// <summary>
/// Shared per-tick decision for continuous-scan handheld analyzers (health, reagent, plant, ...).
/// Mirrors upstream <c>HealthAnalyzerSystem.Update</c> so the fork's copies cannot drift: rate-limit,
/// drop deleted/invalid targets, pause when the target leaves range, otherwise push live state.
///
/// The helper only makes the decision; the caller owns the component fields and performs the
/// matching side effect (stop / pause / push) for its own analyzer type. This keeps the loop's
/// ordering and the "advance the timer only after the rate-limit passes and the target is still
/// valid" rule in one place, while respecting each component's <c>[Access]</c> lock.
/// </summary>
public static class ScannerUpdateHelper
{
    public enum ScanAction
    {
        /// <summary>Not yet time to update, or no target pinned. The caller does nothing.</summary>
        Idle,

        /// <summary>Target deleted or no longer valid. The caller should stop the scan.</summary>
        Drop,

        /// <summary>Target out of range this tick. The caller should pause (ship the "paused" state once).</summary>
        Pause,

        /// <summary>Target present and in range. The caller should push an active update.</summary>
        Push,
    }

    /// <summary>
    /// What an analyzer should do this tick, plus the value to write back to its update-timer field.
    /// </summary>
    /// <param name="Action">The side effect the caller should perform.</param>
    /// <param name="NextUpdate">
    /// The timer the caller should store. Advanced only once the rate-limit passes and the target is
    /// still valid (matching upstream's ordering where a deleted target leaves the timer untouched);
    /// otherwise left equal to the supplied current timer, so writing it back unconditionally is safe.
    /// </param>
    public readonly record struct ScanResult(ScanAction Action, TimeSpan NextUpdate);

    /// <summary>
    /// Decide what a single analyzer should do this tick.
    /// </summary>
    public static ScanResult Evaluate(
        SharedTransformSystem transform,
        TimeSpan now,
        EntityUid? target,
        TimeSpan nextUpdate,
        TimeSpan updateInterval,
        float? maxRange,
        EntityCoordinates analyzerCoords,
        Func<EntityUid, bool> isTargetGone,
        Func<EntityUid, EntityCoordinates> getTargetCoords)
    {
        if (target is not { } targetUid)
            return new ScanResult(ScanAction.Idle, nextUpdate);

        if (nextUpdate > now)
            return new ScanResult(ScanAction.Idle, nextUpdate);

        if (isTargetGone(targetUid))
            return new ScanResult(ScanAction.Drop, nextUpdate);

        var advanced = now + updateInterval;

        // null max range means infinite range.
        if (maxRange != null && !transform.InRange(getTargetCoords(targetUid), analyzerCoords, maxRange.Value))
            return new ScanResult(ScanAction.Pause, advanced);

        return new ScanResult(ScanAction.Push, advanced);
    }
}
