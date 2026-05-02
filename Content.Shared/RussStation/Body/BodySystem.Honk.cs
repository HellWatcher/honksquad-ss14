// HONK Fork-side partial extension on the upstream BodySystem so fork code can mutate
// fields of BodyComponent that are gated by [Access(typeof(BodySystem))]. Currently used
// to opt the body's organ container out of light occlusion for cybernetic flashlight eyes.

namespace Content.Shared.Body;

public sealed partial class BodySystem
{
    /// <summary>
    /// HONK Sets whether the body's organ container occludes light from contained entities.
    /// Subdermal implants do the equivalent on their dedicated implant slot; we need it on the
    /// body's organ container so a PointLight on a cybernetic eye organ reaches the world.
    /// </summary>
    public void SetOrgansOccludeLight(Entity<BodyComponent> body, bool value)
    {
        if (body.Comp.Organs is { } organs)
            organs.OccludesLight = value;
    }
}
