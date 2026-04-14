using Content.Shared.Radiation.Components;

namespace Content.Shared.Radiation.Systems;

// HONK - Fork-side partial that exposes Slope/Enabled setters for
// RadiationSourceComponent without modifying the upstream file. Type-permission
// access checks pass because partial classes share a single CLR type identity.
public abstract partial class SharedRadiationSystem
{
    public void SetSlope(Entity<RadiationSourceComponent?> entity, float slope)
    {
        if (!SourceQuery.Resolve(entity, ref entity.Comp, false))
            return;

        entity.Comp.Slope = slope;
        UpdateSource((entity, entity.Comp));
    }

    public void SetEnabled(Entity<RadiationSourceComponent?> entity, bool enabled)
    {
        if (!SourceQuery.Resolve(entity, ref entity.Comp, false))
            return;

        entity.Comp.Enabled = enabled;
        UpdateSource((entity, entity.Comp));
    }
}
