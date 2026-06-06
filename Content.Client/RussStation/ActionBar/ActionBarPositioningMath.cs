namespace Content.Client.RussStation.ActionBar;

// HONK Pure positioning math pulled out of ActionBarPositioningManager so the drag-clamp
// behaviour can be unit-tested without a live ActionsBar widget. See issue #855.
public static class ActionBarPositioningMath
{
    /// <summary>Clamps a single axis of the bar's free position after a drag delta so the bar
    /// stays inside its container with <paramref name="margin"/> pixels of breathing room on
    /// each edge. The upper bound is floored at <paramref name="margin"/> via Max so a bar that
    /// is wider/taller than its container (where <c>bounds - size - margin</c> goes negative)
    /// pins to the near edge instead of throwing on an inverted clamp range.</summary>
    public static float ClampAxis(float current, float delta, float size, float bounds, float margin)
    {
        return Math.Clamp(current + delta, margin, MathF.Max(margin, bounds - size - margin));
    }
}
