// HONK - issue #609. Audible highlight pings on radio. Owns its own plain-text
// regex list so the upstream visual matcher (which targets WrappedMessage with
// sanitized brackets and `says, "..."` lookbehinds) stays untouched.
using System.Text.RegularExpressions;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Robust.Client.Audio;
using Robust.Client.Replays.Playback;
using Robust.Client.UserInterface;
using Robust.Shared.Audio;
using Robust.Shared.Player;

namespace Content.Client.UserInterface.Systems.Chat;

public sealed partial class ChatUIController
{
    [Dependency] private readonly IReplayPlaybackManager _replayPlayback = default!;
    [UISystemDependency] private readonly AudioSystem _honkAudio = default!;

    private readonly List<Regex> _honkHighlightsPlain = new();

    private bool _honkHighlightSoundEnabled;
    private string _honkHighlightSoundPath = string.Empty;
    private float _honkHighlightSoundVolume;
    private float _honkHighlightSoundCooldown;
    private TimeSpan _honkHighlightSoundNextAllowed;

    private void HonkInitializeHighlightSound()
    {
        _config.OnValueChanged(CCVars.ChatHighlightSoundEnabled, v => _honkHighlightSoundEnabled = v, true);
        _config.OnValueChanged(CCVars.ChatHighlightSoundPath, v => _honkHighlightSoundPath = v, true);
        _config.OnValueChanged(CCVars.ChatHighlightSoundVolume, v => _honkHighlightSoundVolume = v, true);
        _config.OnValueChanged(CCVars.ChatHighlightSoundCooldown, v => _honkHighlightSoundCooldown = v, true);

        _config.OnValueChanged(CCVars.ChatHighlights, HonkRebuildPlainHighlights, true);
    }

    private void HonkRebuildPlainHighlights(string raw)
    {
        _honkHighlightsPlain.Clear();
        if (string.IsNullOrEmpty(raw))
            return;

        foreach (var entry in raw.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            // Drop the leading "@" used by the visual matcher to anchor a name to a
            // speech context: in raw msg.Message there's no "says," wrapper, so a
            // bare substring match is what we want.
            var word = entry.StartsWith('@') ? entry[1..] : entry;
            // Quoted entries become whole-word in the visual matcher; mirror that
            // intent here cheaply by collapsing surrounding quotes into \b.
            var quoted = word.Length >= 2 && word[0] == '"' && word[^1] == '"';
            if (quoted)
                word = word[1..^1];
            if (string.IsNullOrEmpty(word))
                continue;

            var pattern = Regex.Escape(word);
            if (quoted)
                pattern = $@"\b{pattern}\b";
            _honkHighlightsPlain.Add(new Regex(pattern, RegexOptions.IgnoreCase));
        }
    }

    private void HonkTryPlayHighlightSound(ChatMessage msg)
    {
        if (!_honkHighlightSoundEnabled)
            return;
        if (_honkHighlightsPlain.Count == 0)
            return;
        if ((msg.Channel & ChatChannel.Radio) == 0)
            return;
        if (_replayPlayback.Replay != null)
            return;
        if (_timing.RealTime < _honkHighlightSoundNextAllowed)
            return;
        // Don't ping on your own transmissions.
        var localUid = _player.LocalEntity;
        if (localUid != null && _ent.GetEntity(msg.SenderEntity) == localUid.Value)
            return;

        foreach (var pattern in _honkHighlightsPlain)
        {
            if (!pattern.IsMatch(msg.Message))
                continue;
            _honkAudio.PlayGlobal(
                _honkHighlightSoundPath,
                Filter.Local(),
                false,
                AudioParams.Default.WithVolume(_honkHighlightSoundVolume));
            _honkHighlightSoundNextAllowed = _timing.RealTime + TimeSpan.FromSeconds(_honkHighlightSoundCooldown);
            return;
        }
    }
}
