// HONK — covers the three decision helpers extracted from the floating
// chat input. The widget itself needs a full UI harness to exercise, so
// these tests pin the pure logic only. See issue #577.

using Content.Client.RussStation.Chat;
using Content.Shared.Chat;
using NUnit.Framework;

namespace Content.Tests.Client.RussStation.Chat;

[TestFixture]
[TestOf(typeof(FloatingChatInputRouting))]
public sealed class FloatingChatInputRoutingTest
{
    // ---------- BuildRadioPrefixedText ----------

    [Test]
    public void BuildRadioPrefixedText_NoPending_UsesCommonPrefix()
    {
        var output = FloatingChatInputRouting.BuildRadioPrefixedText("hello", null);
        Assert.That(output, Is.EqualTo(";hello"));
    }

    [Test]
    public void BuildRadioPrefixedText_NullKeycodeChar_UsesCommonPrefix()
    {
        // Defensive: a prototype exists but exposes no keycode — treat as common.
        var output = FloatingChatInputRouting.BuildRadioPrefixedText("hello", '\0');
        Assert.That(output, Is.EqualTo(";hello"));
    }

    [Test]
    public void BuildRadioPrefixedText_PendingKeycode_RoutesViaChannelPrefix()
    {
        var output = FloatingChatInputRouting.BuildRadioPrefixedText("hello", 'h');
        Assert.That(output, Is.EqualTo(":h hello"));
    }

    // ---------- ResolveDefaultChannel ----------

    [Test]
    public void ResolveDefaultChannel_RememberOff_AlwaysReturnsLocal()
    {
        var result = FloatingChatInputRouting.ResolveDefaultChannel(
            rememberEnabled: false,
            storedRaw: (int) ChatSelectChannel.Radio,
            selectable: ChatSelectChannel.Radio | ChatSelectChannel.Local);

        Assert.That(result, Is.EqualTo(ChatSelectChannel.Local));
    }

    [Test]
    public void ResolveDefaultChannel_RememberOn_Selectable_RestoresStored()
    {
        var result = FloatingChatInputRouting.ResolveDefaultChannel(
            rememberEnabled: true,
            storedRaw: (int) ChatSelectChannel.Radio,
            selectable: ChatSelectChannel.Radio | ChatSelectChannel.Local);

        Assert.That(result, Is.EqualTo(ChatSelectChannel.Radio));
    }

    [Test]
    public void ResolveDefaultChannel_RememberOn_NotSelectable_FallsBackToLocal()
    {
        // Player had Dead (as a ghost) but is now alive — Dead isn't in the mask.
        var result = FloatingChatInputRouting.ResolveDefaultChannel(
            rememberEnabled: true,
            storedRaw: (int) ChatSelectChannel.Dead,
            selectable: ChatSelectChannel.Local | ChatSelectChannel.OOC);

        Assert.That(result, Is.EqualTo(ChatSelectChannel.Local));
    }

    [Test]
    public void ResolveDefaultChannel_RememberOn_StoredIsNone_FallsBackToLocal()
    {
        // Fresh CVar — never submitted, stored value is 0.
        var result = FloatingChatInputRouting.ResolveDefaultChannel(
            rememberEnabled: true,
            storedRaw: 0,
            selectable: ChatSelectChannel.Local | ChatSelectChannel.Radio);

        Assert.That(result, Is.EqualTo(ChatSelectChannel.Local));
    }

    // ---------- ResolveLabelSource ----------

    [Test]
    public void ResolveLabelSource_TypedPrefix_AlwaysWins()
    {
        // Even with a pending radio restored, a typed prefix should paint from the prefix.
        var result = FloatingChatInputRouting.ResolveLabelSource(
            selected: ChatSelectChannel.Radio,
            hasPendingRadio: true,
            prefixChannel: ChatSelectChannel.LOOC);

        Assert.That(result, Is.EqualTo(FloatingChatInputRouting.LabelSource.Prefix));
    }

    [Test]
    public void ResolveLabelSource_RadioWithPending_NoPrefix_UsesPendingRadio()
    {
        var result = FloatingChatInputRouting.ResolveLabelSource(
            selected: ChatSelectChannel.Radio,
            hasPendingRadio: true,
            prefixChannel: ChatSelectChannel.None);

        Assert.That(result, Is.EqualTo(FloatingChatInputRouting.LabelSource.PendingRadio));
    }

    [Test]
    public void ResolveLabelSource_RadioWithoutPending_NoPrefix_UsesSelected()
    {
        // After the user overrides restored state via dropdown, pending is cleared.
        var result = FloatingChatInputRouting.ResolveLabelSource(
            selected: ChatSelectChannel.Radio,
            hasPendingRadio: false,
            prefixChannel: ChatSelectChannel.None);

        Assert.That(result, Is.EqualTo(FloatingChatInputRouting.LabelSource.Selected));
    }

    [Test]
    public void ResolveLabelSource_NonRadio_IgnoresPending()
    {
        var result = FloatingChatInputRouting.ResolveLabelSource(
            selected: ChatSelectChannel.Local,
            hasPendingRadio: true,
            prefixChannel: ChatSelectChannel.None);

        Assert.That(result, Is.EqualTo(FloatingChatInputRouting.LabelSource.Selected));
    }
}
