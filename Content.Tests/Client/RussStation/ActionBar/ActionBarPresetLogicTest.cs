// HONK — pins the pure preset-side decisions extracted from ActionBarPresetManager
// during the controller decomposition. The manager itself needs the YAML store and a
// UI harness; these cover the selection + emote-map logic only. See issue #855.

using System.Collections.Generic;
using Content.Client.RussStation.ActionBar;
using NUnit.Framework;

namespace Content.Tests.Client.RussStation.ActionBar;

[TestFixture]
[TestOf(typeof(ActionBarPresetLogic))]
public sealed class ActionBarPresetLogicTest
{
    private static ActionBarPreset Preset(string name, string character)
        => new() { Name = name, CharacterName = character };

    // ---------- SelectForCharacter ----------

    [Test]
    public void SelectForCharacter_EmptyList_ReturnsNull()
    {
        var result = ActionBarPresetLogic.SelectForCharacter(new List<ActionBarPreset>(), "Urist");
        Assert.That(result, Is.Null);
    }

    [Test]
    public void SelectForCharacter_ExactMatch_ReturnsIt()
    {
        var presets = new List<ActionBarPreset>
        {
            Preset("a", "Other"),
            Preset("b", "Urist"),
        };

        var result = ActionBarPresetLogic.SelectForCharacter(presets, "Urist");
        Assert.That(result?.Name, Is.EqualTo("b"));
    }

    [Test]
    public void SelectForCharacter_NoExactMatch_FallsBackToCharacterAgnostic()
    {
        var presets = new List<ActionBarPreset>
        {
            Preset("a", "Other"),
            Preset("global", string.Empty),
        };

        var result = ActionBarPresetLogic.SelectForCharacter(presets, "Urist");
        Assert.That(result?.Name, Is.EqualTo("global"));
    }

    [Test]
    public void SelectForCharacter_ExactMatchWins_EvenWhenAgnosticIsFirst()
    {
        // The character-agnostic preset comes first, but an exact match should still win.
        var presets = new List<ActionBarPreset>
        {
            Preset("global", string.Empty),
            Preset("mine", "Urist"),
        };

        var result = ActionBarPresetLogic.SelectForCharacter(presets, "Urist");
        Assert.That(result?.Name, Is.EqualTo("mine"));
    }

    [Test]
    public void SelectForCharacter_NoMatchAndNoAgnostic_ReturnsNull()
    {
        var presets = new List<ActionBarPreset>
        {
            Preset("a", "Other"),
            Preset("b", "Someone"),
        };

        var result = ActionBarPresetLogic.SelectForCharacter(presets, "Urist");
        Assert.That(result, Is.Null);
    }

    [Test]
    public void SelectForCharacter_EmptyCharacterName_MatchesAgnosticPresetExactly()
    {
        // Before prefs sync the active name is empty; that should match a preset saved with an
        // empty CharacterName through the exact-match branch, not just the fallback.
        var presets = new List<ActionBarPreset>
        {
            Preset("a", "Other"),
            Preset("global", string.Empty),
        };

        var result = ActionBarPresetLogic.SelectForCharacter(presets, string.Empty);
        Assert.That(result?.Name, Is.EqualTo("global"));
    }

    [Test]
    public void SelectForCharacter_IsCaseSensitive()
    {
        var presets = new List<ActionBarPreset> { Preset("a", "Urist") };

        var result = ActionBarPresetLogic.SelectForCharacter(presets, "urist");
        Assert.That(result, Is.Null);
    }

    // ---------- BuildEmoteSlotMap ----------

    [Test]
    public void BuildEmoteSlotMap_SkipsNullAndEmptyEntries()
    {
        var emotes = new List<string?> { null, "Wave", string.Empty, "Salute" };

        var map = ActionBarPresetLogic.BuildEmoteSlotMap(emotes, _ => true);

        Assert.That(map, Has.Count.EqualTo(2));
        Assert.That(map["Wave"], Is.EqualTo(1));
        Assert.That(map["Salute"], Is.EqualTo(3));
    }

    [Test]
    public void BuildEmoteSlotMap_DropsInvalidEmotes()
    {
        var emotes = new List<string?> { "Wave", "GoneEmote", "Salute" };
        var valid = new HashSet<string> { "Wave", "Salute" };

        var map = ActionBarPresetLogic.BuildEmoteSlotMap(emotes, valid.Contains);

        Assert.That(map.ContainsKey("GoneEmote"), Is.False);
        Assert.That(map["Wave"], Is.EqualTo(0));
        Assert.That(map["Salute"], Is.EqualTo(2));
    }

    [Test]
    public void BuildEmoteSlotMap_DuplicateId_LastSlotWins()
    {
        // Two slots hold the same emote; the later index overwrites, matching the original
        // dictionary-assignment behaviour.
        var emotes = new List<string?> { "Wave", "Wave" };

        var map = ActionBarPresetLogic.BuildEmoteSlotMap(emotes, _ => true);

        Assert.That(map["Wave"], Is.EqualTo(1));
    }

    [Test]
    public void BuildEmoteSlotMap_Empty_ReturnsEmptyMap()
    {
        var map = ActionBarPresetLogic.BuildEmoteSlotMap(new List<string?>(), _ => true);
        Assert.That(map, Is.Empty);
    }
}
