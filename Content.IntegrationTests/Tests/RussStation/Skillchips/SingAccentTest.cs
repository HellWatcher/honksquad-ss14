using Content.Server.RussStation.Skillchips;
using Content.Shared.RussStation.Skillchips;
using Content.Shared.Speech;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.RussStation.Skillchips;

/// <summary>
/// Verifies <see cref="SingAccentSystem"/> reshapes the final word of a message into a "sung"
/// form: the last vowel is held (repeated) and the line ends on emphatic punctuation.
/// Granted by the SS13 Musical skillchip via <see cref="SingAccentComponent"/>.
/// </summary>
[TestFixture]
[TestOf(typeof(SingAccentSystem))]
public sealed class SingAccentTest
{
    [Test]
    public async Task SingifiesFinalWordTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var singer = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            entMan.AddComponent<SingAccentComponent>(singer);

            var ev = new AccentGetEvent(singer, "hello world.");
            entMan.EventBus.RaiseLocalEvent(singer, ref ev);

            // "world." -> hold the 'o' -> "woooorld" and the trailing period becomes "!".
            Assert.That(ev.Message, Does.StartWith("hello "), "Only the final word should be reshaped.");
            Assert.That(ev.Message, Does.Contain("oooo"), "The last vowel of the final word should be held.");
            Assert.That(ev.Message, Does.EndWith("!"), "A trailing period should become an exclamation mark.");

            entMan.DeleteEntity(singer);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AddsExclamationWhenNoPunctuationTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var singer = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            entMan.AddComponent<SingAccentComponent>(singer);

            var ev = new AccentGetEvent(singer, "do re mi");
            entMan.EventBus.RaiseLocalEvent(singer, ref ev);

            Assert.That(ev.Message, Does.EndWith("!"), "Final word without punctuation should gain an exclamation mark.");
            Assert.That(ev.Message, Does.Contain("miii"), "The vowel 'i' in the final word should be held.");

            entMan.DeleteEntity(singer);
        });

        await pair.CleanReturnAsync();
    }
}
