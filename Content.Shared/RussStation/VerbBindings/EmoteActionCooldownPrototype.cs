using Robust.Shared.Prototypes;

namespace Content.Shared.RussStation.VerbBindings;

/// HONK Per-emote cooldown applied to the auto-granted HonkActionEmote.
/// Prototype id matches the EmotePrototype id whose action button gets the delay.
[Prototype]
public sealed partial class EmoteActionCooldownPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public TimeSpan Cooldown;
}
