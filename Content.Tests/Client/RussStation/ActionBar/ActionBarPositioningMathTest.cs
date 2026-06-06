// HONK — pins the drag-clamp math extracted from ActionBarPositioningManager during the
// controller decomposition. The reparenting itself needs a live ActionsBar widget; this
// covers the per-axis clamp only, including the oversized-bar guard. See issue #855.

using Content.Client.RussStation.ActionBar;
using NUnit.Framework;

namespace Content.Tests.Client.RussStation.ActionBar;

[TestFixture]
[TestOf(typeof(ActionBarPositioningMath))]
public sealed class ActionBarPositioningMathTest
{
    private const float Margin = 4f;

    [Test]
    public void ClampAxis_WithinBounds_AppliesDelta()
    {
        var result = ActionBarPositioningMath.ClampAxis(current: 100f, delta: 10f, size: 50f, bounds: 500f, margin: Margin);
        Assert.That(result, Is.EqualTo(110f).Within(0.001f));
    }

    [Test]
    public void ClampAxis_BelowMargin_ClampsToMargin()
    {
        var result = ActionBarPositioningMath.ClampAxis(current: 2f, delta: -10f, size: 50f, bounds: 500f, margin: Margin);
        Assert.That(result, Is.EqualTo(Margin).Within(0.001f));
    }

    [Test]
    public void ClampAxis_PastFarEdge_ClampsToBoundsMinusSizeMinusMargin()
    {
        // Upper bound = 500 - 50 - 4 = 446.
        var result = ActionBarPositioningMath.ClampAxis(current: 440f, delta: 20f, size: 50f, bounds: 500f, margin: Margin);
        Assert.That(result, Is.EqualTo(446f).Within(0.001f));
    }

    [Test]
    public void ClampAxis_BarLargerThanBounds_PinsToMargin()
    {
        // bounds - size - margin goes negative (500 - 600 - 4 = -104); the Max guard floors the
        // upper bound at the margin so the clamp range stays valid and the bar pins to the edge.
        var result = ActionBarPositioningMath.ClampAxis(current: 100f, delta: 0f, size: 600f, bounds: 500f, margin: Margin);
        Assert.That(result, Is.EqualTo(Margin).Within(0.001f));
    }
}
