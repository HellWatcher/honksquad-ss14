using Content.Shared.Chat.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.RussStation.VerbBindings;

/// <summary>
/// HONK Curated list of emotes that become per-emote action entities granted to every
/// player-controlled mob. Gate for the #579 emote-as-action feature so the actions menu
/// isn't flooded with every EmotePrototype in the game.
/// </summary>
[Prototype("honkEmoteActionAllowlist")]
public sealed partial class HonkEmoteActionAllowlistPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Emotes that should become player-usable action entities.</summary>
    [DataField(required: true)]
    public List<ProtoId<EmotePrototype>> Emotes = new();
}
