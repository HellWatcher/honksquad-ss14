using Content.Shared.CCVar;

namespace Content.Client.UserInterface.Systems.Chat;

// HONK - Fork partial adding curated highlight preset lists (issue #610).
// Presets live next to the user's hand-typed list: toggling a preset injects
// its word list into the compiled highlight regex without polluting the saved
// user-entered highlights string.
public sealed partial class ChatUIController
{
    private bool _presetAntagsEnabled;
    private bool _presetDistressEnabled;

    public event Action? HighlightPresetsChanged;

    partial void InitializePresetHighlights()
    {
        _config.OnValueChanged(CCVars.HonkChatHighlightPresetAntags, value =>
        {
            _presetAntagsEnabled = value;
            RebuildHighlightsFromSaved();
            HighlightPresetsChanged?.Invoke();
        }, true);

        _config.OnValueChanged(CCVars.HonkChatHighlightPresetDistress, value =>
        {
            _presetDistressEnabled = value;
            RebuildHighlightsFromSaved();
            HighlightPresetsChanged?.Invoke();
        }, true);
    }

    partial void AppendActivePresetHighlights(List<string> highlights)
    {
        if (_presetAntagsEnabled)
            AppendPresetWords("highlight-preset-antags", highlights);

        if (_presetDistressEnabled)
            AppendPresetWords("highlight-preset-distress", highlights);
    }

    private void AppendPresetWords(string locKey, List<string> highlights)
    {
        if (!_loc.TryGetString(locKey, out var raw))
            return;

        foreach (var line in raw.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!highlights.Contains(line))
                highlights.Add(line);
        }
    }

    /// <summary>
    /// Recompiles the highlight regex list from the saved user text plus any active presets.
    /// Call after toggling a preset so its words take effect immediately.
    /// </summary>
    private void RebuildHighlightsFromSaved()
    {
        var saved = _config.GetCVar(CCVars.ChatHighlights);
        UpdateHighlights(saved, firstLoad: true);
    }
}
