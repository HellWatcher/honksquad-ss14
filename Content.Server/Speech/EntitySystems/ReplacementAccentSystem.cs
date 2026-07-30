using System.Linq;
using System.Text.RegularExpressions;
using Content.Server.Speech.Components;
using Content.Server.Speech.Prototypes;
using Content.Shared.Speech;
using Content.Shared.Speech.EntitySystems;
// HONK START - #634: Accentless strip-list event handling.
using Content.Shared.Traits.Assorted;
// HONK END
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Speech.EntitySystems;

// TODO: Code in-game languages and make this a language
/// <summary>
/// Replaces text in messages, either with full replacements or word replacements.
/// </summary>
public sealed partial class ReplacementAccentSystem : RelayAccentSystem<ReplacementAccentComponent>
{
    [Dependency] private IRobustRandom _random = default!;

    // HONK START - #481: cache tuple gains a per-word chance (null = use global).
    private readonly Dictionary<ProtoId<ReplacementAccentPrototype>, (Regex regex, string replacement, float? chance)[]>
        _cachedReplacements = new();
    // HONK END

    public override void Initialize()
    {
        base.Initialize();

        // HONK START - #634: fold legacy single `accent:` yaml field into the list so callers only read Accents.
        SubscribeLocalEvent<ReplacementAccentComponent, ComponentInit>(OnInit);
        // Accentless dispatches this when a per-species strip list fires; handled here because
        // ReplacementAccentComponent lives in Content.Server.
        SubscribeLocalEvent<ReplacementAccentComponent, AccentlessStripReplacementAccentsEvent>(OnAccentlessStrip);
        // HONK END

        ProtoMan.PrototypesReloaded += OnPrototypesReloaded;
    }

    // HONK START - #634
    private void OnInit(EntityUid uid, ReplacementAccentComponent component, ComponentInit args)
    {
        if (component.Accent == null)
            return;

        var id = new ProtoId<ReplacementAccentPrototype>(component.Accent);
        if (!component.Accents.Contains(id))
            component.Accents.Add(id);
        component.Accent = null;
    }

    private void OnAccentlessStrip(EntityUid uid, ReplacementAccentComponent component, ref AccentlessStripReplacementAccentsEvent args)
    {
        foreach (var accentId in args.AccentIds)
            component.Accents.Remove(accentId);
    }
    // HONK END

    public override void Shutdown()
    {
        base.Shutdown();

        ProtoMan.PrototypesReloaded -= OnPrototypesReloaded;
    }

    public override string Accentuate(string message, Entity<ReplacementAccentComponent>? ent = null)
    {
        if (ent == null)
            return message;

        // HONK START - #634: iterate list of accents; random-pick among stacked full-replacement entries.
        var component = ent.Value.Comp;
        if (component.Accents.Count == 0)
            return message;

        var fullReplacementAccents = new List<ProtoId<ReplacementAccentPrototype>>();
        foreach (var accent in component.Accents)
        {
            if (ProtoMan.TryIndex(accent, out var proto) && proto.FullReplacements != null)
                fullReplacementAccents.Add(accent);
        }

        if (fullReplacementAccents.Count > 0)
        {
            var winner = _random.Pick(fullReplacementAccents);
            foreach (var accent in component.Accents)
            {
                if (fullReplacementAccents.Contains(accent) && accent != winner)
                    continue;

                message = ApplyReplacements(message, accent);
            }
            return message;
        }

        foreach (var accent in component.Accents)
            message = ApplyReplacements(message, accent);

        return message;
        // HONK END
    }

    /// <summary>
    ///     Attempts to apply a given replacement accent prototype to a message.
    /// </summary>
    [PublicAPI]
    public string ApplyReplacements(string message, string accent)
    {
        if (!ProtoMan.TryIndex<ReplacementAccentPrototype>(accent, out var prototype))
            return message;

        if (!_random.Prob(prototype.ReplacementChance))
            return message;

        // Prioritize fully replacing if that exists--
        // ideally both aren't used at the same time (but we don't have a way to enforce that in serialization yet)
        if (prototype.FullReplacements != null)
        {
            return prototype.FullReplacements.Length != 0 ? Loc.GetString(_random.Pick(prototype.FullReplacements)) : "";
        }

        // Prohibition of repeated word replacements.
        // All replaced words placed in the final message are placed here as dashes (___) with the same length.
        // The regex search goes through this buffer message, from which the already replaced words are crossed out,
        // ensuring that the replaced words cannot be replaced again.
        var maskMessage = message;

        // HONK START - #481: tuple gains per-word chance, plus an inner roll that masks
        // a non-replaced match so it doesn't get reconsidered next iteration. Block wraps
        // the whole loop because the foreach signature change (third tuple element) and
        // the inner chance check both belong to the same fork patch.
        foreach (var (regex, replace, perWordChance) in GetCachedReplacements(prototype))
        {
            // this is kind of slow but its not that bad
            // essentially: go over all matches, try to match capitalization where possible, then replace
            // rather than using regex.replace
            for (int i = regex.Count(maskMessage); i > 0; i--)
            {
                // fetch the match again as the character indices may have changed
                Match match = regex.Match(maskMessage);
                var replacement = replace;

                if (perWordChance is { } chance && !_random.Prob(chance))
                {
                    var skipMask = new string('_', match.Length);
                    maskMessage = maskMessage.Remove(match.Index, match.Length).Insert(match.Index, skipMask);
                    continue;
                }

                // Intelligently replace capitalization
                // two cases where we will do so:
                // - the string is all upper case (just uppercase the replacement too)
                // - the first letter of the word is capitalized (common, just uppercase the first letter too)
                // any other cases are not really useful or not viable, since the match & replacement can be different
                // lengths

                // second expression here is weird--its specifically for single-word capitalization for I or A
                // dwarf expands I -> Ah, without that it would transform I -> AH
                // so that second case will only fully-uppercase if the replacement length is also 1
                if (!match.Value.Any(char.IsLower) && (match.Length > 1 || replacement.Length == 1))
                {
                    replacement = replacement.ToUpperInvariant();
                }
                else if (match.Length >= 1 && replacement.Length >= 1 && char.IsUpper(match.Value[0]))
                {
                    replacement = replacement[0].ToString().ToUpper() + replacement[1..];
                }

                // In-place replace the match with the transformed capitalization replacement
                message = message.Remove(match.Index, match.Length).Insert(match.Index, replacement);
                var mask = new string('_', replacement.Length);
                maskMessage = maskMessage.Remove(match.Index, match.Length).Insert(match.Index, mask);
            }
        }
        // HONK END
        return message;
    }

    // HONK START - #481: cache tuple includes optional per-word chance.
    private (Regex regex, string replacement, float? chance)[] GetCachedReplacements(ReplacementAccentPrototype prototype)
    {
        if (!_cachedReplacements.TryGetValue(prototype.ID, out var replacements))
        {
            replacements = GenerateCachedReplacements(prototype);
            _cachedReplacements.Add(prototype.ID, replacements);
        }
        return replacements;
    }

    private (Regex regex, string replacement, float? chance)[] GenerateCachedReplacements(ReplacementAccentPrototype prototype)
    {
        if (prototype.WordReplacements is not { } replacements)
            return [];

        return replacements.Select(kv =>
            {
                var (first, replace) = kv;
                var firstLoc = Loc.GetString(first);
                var replaceLoc = Loc.GetString(replace);

                var regex = new Regex($@"(?<![\w']){firstLoc}(?![\w'])", RegexOptions.IgnoreCase);

                float? chance = null;
                if (prototype.WordReplacementChances is { } chances && chances.TryGetValue(first, out var c))
                    chance = c;

                return (regex, replaceLoc, chance);

            })
            .ToArray();
    }
    // HONK END

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs obj)
    {
        _cachedReplacements.Clear();
    }
}
