using Content.Server.Speech.Prototypes;
using Content.Shared.Speech.Components;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
// HONK START - #634: ProtoId<T> for the new Accents list.
using Robust.Shared.Prototypes;
// HONK END

namespace Content.Server.Speech.Components;

/// <summary>
/// Replaces full sentences or words within sentences with new strings.
/// </summary>
[RegisterComponent]
public sealed partial class ReplacementAccentComponent : BaseAccentComponent
{
    // HONK START - #634: field split so accents compose. Legacy `accent: X` still deserializes and gets merged
    // into Accents on ComponentInit. At least one of the two must be provided (validated at init).
    [DataField("accent", customTypeSerializer: typeof(PrototypeIdSerializer<ReplacementAccentPrototype>))]
    public string? Accent;

    [DataField("accents")]
    public List<ProtoId<ReplacementAccentPrototype>> Accents = new();
    // HONK END
}
