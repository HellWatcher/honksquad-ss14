using Content.Client.Gameplay;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.Chat.Prototypes;
using Content.Shared.RussStation.Input;
using JetBrains.Annotations;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Configuration;
using Robust.Shared.Input.Binding;
using Robust.Shared.Prototypes;

namespace Content.Client.RussStation.VerbBindings;

// HONK First slice of the verb-bindings feature (#579). This controller
// owns the 20 HonkVerbBindN key functions: it reads the slot→emote map
// from the honk.verb_bindings.slots CVar and, when a slot key fires,
// dispatches PlayEmoteMessage just like the emotes menu would.
//
// Only emotes are supported in this slice. The curated gameplay-verb
// allowlist and the UI for picking slot bindings land in follow-up commits.
[UsedImplicitly]
public sealed class VerbBindingExecutionController : UIController,
    IOnStateEntered<GameplayState>, IOnStateExited<GameplayState>
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPrototypeManager _protoMan = default!;

    private readonly Dictionary<int, string> _slots = new();

    public override void Initialize()
    {
        base.Initialize();
        _cfg.OnValueChanged(CCVars.HonkVerbBindingSlots, ParseSlots, true);
    }

    public void OnStateEntered(GameplayState state)
    {
        var builder = CommandBinds.Builder;
        for (var i = 0; i < HonkVerbBindKeyFunctions.SlotCount; i++)
        {
            var slot = i;
            builder = builder.Bind(
                HonkVerbBindKeyFunctions.All[slot],
                InputCmdHandler.FromDelegate(_ => FireSlot(slot)));
        }
        builder.Register<VerbBindingExecutionController>();
    }

    public void OnStateExited(GameplayState state)
    {
        CommandBinds.Unregister<VerbBindingExecutionController>();
    }

    private void ParseSlots(string raw)
    {
        _slots.Clear();
        if (string.IsNullOrWhiteSpace(raw))
            return;

        foreach (var entry in raw.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = entry.IndexOf('=');
            if (eq <= 0 || eq == entry.Length - 1)
                continue;

            if (!int.TryParse(entry.AsSpan(0, eq), out var slot))
                continue;
            if (slot < 0 || slot >= HonkVerbBindKeyFunctions.SlotCount)
                continue;

            _slots[slot] = entry[(eq + 1)..];
        }
    }

    private void FireSlot(int slot)
    {
        if (!_slots.TryGetValue(slot, out var protoId))
            return;
        if (!_protoMan.HasIndex<EmotePrototype>(protoId))
            return;

        EntityManager.RaisePredictiveEvent(new PlayEmoteMessage(protoId));
    }
}
