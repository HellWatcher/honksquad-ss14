// HONK Fork-side partial extension on the upstream MetabolizerSystem so fork code can write
// to MetabolizerComponent.MetabolizerTypes without tripping HONK0003. The field is already in
// MetabolizerComponent's [Access] friend list (HONK-marked); this just keeps the write inside
// the owning system rather than scattering it across RussStation files.

using Robust.Shared.Prototypes;

namespace Content.Shared.Metabolism;

public sealed partial class MetabolizerSystem
{
    /// <summary>
    /// HONK Replaces the metabolizer's type set in-place. Used by SharedCyberneticLungsSystem to
    /// inherit host-body metabolizer types onto a freshly-inserted cybernetic lung so the
    /// Oxygenate condition resolves for any species.
    /// </summary>
    public void SetMetabolizerTypes(Entity<MetabolizerComponent> metabolizer, HashSet<ProtoId<MetabolizerTypePrototype>> types)
    {
        metabolizer.Comp.MetabolizerTypes = types;
        Dirty(metabolizer);
    }
}
