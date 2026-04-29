using Content.Shared.RussStation.Skillchips;
using Content.Shared.Speech;

namespace Content.Server.RussStation.Skillchips;

/// <summary>
/// Reshapes speech to sound sung: holds the last vowel of the final word and adds punctuation.
/// Applied when a mob has SingAccentComponent (granted by the SS13 Musical skillchip).
/// </summary>
public sealed class SingAccentSystem : EntitySystem
{
    private static readonly char[] Vowels = ['a', 'e', 'i', 'o', 'u'];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SingAccentComponent, AccentGetEvent>(OnAccentGet);
    }

    private void OnAccentGet(Entity<SingAccentComponent> ent, ref AccentGetEvent args)
    {
        args.Message = Singify(args.Message);
    }

    private static string Singify(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return message;

        var words = message.Split(' ');
        var last = words[^1];
        words[^1] = ReshapeWord(last);
        return string.Join(' ', words);
    }

    private static string ReshapeWord(string word)
    {
        if (string.IsNullOrEmpty(word))
            return word;

        // Find the last vowel in the word.
        var lastVowelIndex = -1;
        for (var i = word.Length - 1; i >= 0; i--)
        {
            if (Array.IndexOf(Vowels, char.ToLower(word[i])) >= 0)
            {
                lastVowelIndex = i;
                break;
            }
        }

        if (lastVowelIndex < 0)
            return word + "~";

        // Hold the vowel by repeating it four times.
        var result = word[..lastVowelIndex]
                     + new string(word[lastVowelIndex], 4)
                     + word[(lastVowelIndex + 1)..];

        // Replace a trailing period with an exclamation mark; add one if missing.
        if (result.EndsWith('.') && !result.EndsWith("..."))
            result = result[..^1] + "!";
        else if (!result.EndsWith('!') && !result.EndsWith('?') && !result.EndsWith("..."))
            result += "!";

        return result;
    }
}
