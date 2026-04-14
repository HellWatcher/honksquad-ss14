using System.Numerics;
using Robust.Shared.Maths;

namespace Content.Client.RussStation.UserInterface;

/// <summary>
/// Shared icon scaling helpers for UI controls that need to fit an arbitrary
/// pixel-sized sprite into a fixed display box. Replaces ad-hoc tiered
/// scaling in places like the radial menu and alert panel.
/// </summary>
public static class IconScaling
{
    /// <summary>
    /// Returns a uniform scale that fits a sprite of <paramref name="pixelSize"/>
    /// into a square of <paramref name="targetPx"/> pixels per side, preserving
    /// aspect ratio. Returns <see cref="Vector2.One"/> for empty input so callers
    /// don't have to null-guard.
    /// </summary>
    public static Vector2 FitScale(Vector2i pixelSize, float targetPx)
    {
        var maxSide = Math.Max(pixelSize.X, pixelSize.Y);
        if (maxSide <= 0)
            return Vector2.One;

        var scale = targetPx / maxSide;
        return new Vector2(scale, scale);
    }
}
