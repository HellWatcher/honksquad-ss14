using Content.Client.Lobby;
using Content.Client.UserInterface.Systems.Actions;
using Content.Shared.Chat.Prototypes;
using Robust.Client.UserInterface;
using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;

namespace Content.Client.RussStation.ActionBar;

// HONK Owns the preset side of the action bar: which saved preset matches the active
// character, the emote-slot lookup seeded from it, and the capture / apply / reset round-trip
// with the ActionUIController. The controller delegates here so it doesn't carry the YAML
// store, the preferences read, and the prototype validation itself.
public sealed class ActionBarPresetManager
{
    private readonly IClientPreferencesManager _prefs;
    private readonly IUserInterfaceManager _ui;
    private readonly IPrototypeManager _proto;
    private readonly IResourceManager _resources;
    private readonly ActionBarCVarManager _cvars;
    private readonly ActionBarPositioningManager _positioning;

    private ActionBarPresetStore? _store;

    // Emote id ("Wave", "Salute", ...) -> slot index. Sourced from the active preset's EmoteIds
    // list (loaded at startup and refreshed when ApplyPreset runs) so a player's curated emote
    // layout survives disconnects and server restarts via the preset file rather than a separate
    // CVar. Read by OnActionAdded through TryGetSavedEmoteSlot.
    private readonly Dictionary<string, int> _emoteSlots = new();

    public ActionBarPresetManager(
        IClientPreferencesManager prefs,
        IUserInterfaceManager ui,
        IPrototypeManager proto,
        IResourceManager resources,
        ActionBarCVarManager cvars,
        ActionBarPositioningManager positioning)
    {
        _prefs = prefs;
        _ui = ui;
        _proto = proto;
        _resources = resources;
        _cvars = cvars;
        _positioning = positioning;
    }

    // Lazily allocated so the store doesn't read its file until the player actually has a use
    // for presets; keeps client startup unaffected.
    public ActionBarPresetStore Store => _store ??= new ActionBarPresetStore(_resources);

    private ActionUIController Actions => _ui.GetUIController<ActionUIController>();

    public bool TryGetSavedEmoteSlot(string? emoteId, out int slot)
    {
        if (!string.IsNullOrEmpty(emoteId) && _emoteSlots.TryGetValue(emoteId, out slot))
            return true;
        slot = default;
        return false;
    }

    /// <summary>Currently-selected character profile name, or empty if preferences haven't
    /// synced from the server yet. Empty matches presets that were saved before character
    /// scoping landed (their <c>CharacterName</c> is also empty).</summary>
    public string GetActiveCharacterName()
        => _prefs.Preferences?.SelectedCharacter.Name ?? string.Empty;

    /// <summary>Picks the first saved preset whose <c>CharacterName</c> matches the active
    /// character, falling back to the first character-agnostic preset.</summary>
    public ActionBarPreset? FindActivePresetForCharacter()
        => ActionBarPresetLogic.SelectForCharacter(Store.Presets, GetActiveCharacterName());

    /// <summary>Read the active character's preset and seed <see cref="_emoteSlots"/> from it.
    /// Called during controller Initialize so emote actions granted before HonkOnContainerReady
    /// runs still hit their saved slots.</summary>
    public void LoadActiveEmoteSlots()
    {
        var preset = FindActivePresetForCharacter();
        if (preset == null)
        {
            _emoteSlots.Clear();
            return;
        }
        RefreshEmoteSlots(preset.EmoteIds);
    }

    /// <summary>Refresh <see cref="_emoteSlots"/> from <paramref name="emoteIds"/>, a
    /// parallel-to-SlotProtoIds list where each non-null entry is the emote id that occupies
    /// that slot. Validates against EmotePrototype so a stale preset entry from a removed emote
    /// silently drops out instead of poisoning the bar.</summary>
    private void RefreshEmoteSlots(List<string?> emoteIds)
    {
        _emoteSlots.Clear();
        foreach (var (id, slot) in ActionBarPresetLogic.BuildEmoteSlotMap(emoteIds, id => _proto.HasIndex<EmotePrototype>(id)))
            _emoteSlots[id] = slot;
    }

    public ActionBarPreset CapturePreset()
    {
        var actions = Actions;
        var runtime = ActionBarRuntimeConfig.Current;
        return new ActionBarPreset
        {
            CharacterName = GetActiveCharacterName(),
            Rows = _cvars.Rows,
            SlotsPerRow = _cvars.SlotsPerRow,
            SlotSpacing = _cvars.SlotSpacing,
            ShowKeybindLabel = _cvars.ShowKeybindLabel,
            ShowEmptySlots = runtime.ShowEmptySlots,
            AutoAddActions = runtime.AutoAddActions,
            Lock = runtime.LockActions,
            ButtonBackgroundAlpha = runtime.ButtonBackgroundAlpha,
            PositionX = _positioning.PositionX,
            PositionY = _positioning.PositionY,
            SlotProtoIds = actions.HonkGetSlotProtoIds(),
            EmoteIds = actions.HonkGetSlotEmoteIds(),
        };
    }

    public void ApplyPreset(ActionBarPreset preset)
    {
        _cvars.WritePreset(preset);
        RefreshEmoteSlots(preset.EmoteIds);
        Actions.HonkLoadFromPreset(preset.SlotProtoIds, preset.EmoteIds);
    }

    public void ResetToDefaults()
    {
        // Scope: only reset things the presets window owns (slot contents and bar position).
        // Size/spacing/label/alpha settings live in Options → Misc and have their own controls;
        // wiping them from the preset window's Reset button surprised players who expected only
        // the layout (slot assignments) to revert.
        _cvars.ResetPosition();
        Actions.HonkResetSlots();
    }
}
