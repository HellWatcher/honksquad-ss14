namespace Content.Client.RussStation.ActionBar;

// HONK Pure preset-side decisions pulled out of ActionBarPresetManager so they can be
// unit-tested without IoC, the YAML store, or a UI harness. The manager owns the IO and
// just feeds the loaded data through these. See issue #855.
public static class ActionBarPresetLogic
{
    /// <summary>Picks the preset that should apply for <paramref name="character"/>: the first
    /// one saved against that exact character name, or failing that the first character-agnostic
    /// preset (empty <c>CharacterName</c>, which is how presets saved before character scoping
    /// look). Returns null when neither exists.</summary>
    public static ActionBarPreset? SelectForCharacter(IReadOnlyList<ActionBarPreset> presets, string character)
    {
        if (presets.Count == 0)
            return null;
        foreach (var preset in presets)
        {
            if (string.Equals(preset.CharacterName, character, StringComparison.Ordinal))
                return preset;
        }
        // No exact match: fall through to a character-agnostic preset so old presets and
        // "global" ones still work.
        foreach (var preset in presets)
        {
            if (string.IsNullOrEmpty(preset.CharacterName))
                return preset;
        }
        return null;
    }

    /// <summary>Builds the emote-id -> slot-index lookup from a preset's parallel-to-slots emote
    /// list. Null/empty entries are slots without an emote and are skipped; entries failing
    /// <paramref name="isValidEmote"/> (e.g. a removed prototype) are dropped so a stale preset
    /// can't poison the bar. When the same emote id appears twice the later slot wins, matching
    /// the original last-write-into-dictionary behaviour.</summary>
    public static Dictionary<string, int> BuildEmoteSlotMap(IReadOnlyList<string?> emoteIds, Func<string, bool> isValidEmote)
    {
        var map = new Dictionary<string, int>();
        for (var i = 0; i < emoteIds.Count; i++)
        {
            var id = emoteIds[i];
            if (string.IsNullOrEmpty(id))
                continue;
            if (!isValidEmote(id))
                continue;
            map[id] = i;
        }
        return map;
    }
}
