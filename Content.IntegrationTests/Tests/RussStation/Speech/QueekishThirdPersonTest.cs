using Content.Server.RussStation.Speech.EntitySystems;
using Content.Shared.Speech;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.RussStation.Speech;

/// <summary>
/// Verifies <see cref="QueekishThirdPersonSystem"/> rewrites first-person pronouns to the
/// speaker's first name for a Skaven (e.g. "I am leaving" -> "Thanquol am leaving"), and that
/// the marker is inert on non-Skaven speakers.
/// </summary>
[TestFixture]
[TestOf(typeof(QueekishThirdPersonSystem))]
public sealed class QueekishThirdPersonTest
{
    // HumanoidProfileComponent.Species is write-restricted to HumanoidProfileSystem, so the
    // species is baked into the prototypes rather than assigned from the test.
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: QueekishSkavenDummy
  components:
  - type: HumanoidProfile
    species: Skaven
  - type: QueekishThirdPerson

- type: entity
  id: QueekishHumanDummy
  components:
  - type: HumanoidProfile
    species: Human
  - type: QueekishThirdPerson
";

    [Test]
    public async Task SkavenPronounsBecomeFirstNameTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var metaData = entMan.System<MetaDataSystem>();

        await server.WaitAssertion(() =>
        {
            var speaker = entMan.SpawnEntity("QueekishSkavenDummy", MapCoordinates.Nullspace);
            metaData.SetEntityName(speaker, "Thanquol-Boneripper");

            var ev = new AccentGetEvent(speaker, "I am leaving");
            entMan.EventBus.RaiseLocalEvent(speaker, ref ev);

            Assert.That(ev.Message, Is.EqualTo("Thanquol am leaving"),
                "First-person pronouns should be replaced with the Skaven's first name.");

            entMan.DeleteEntity(speaker);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NonSkavenSpeakerUnchangedTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var metaData = entMan.System<MetaDataSystem>();

        await server.WaitAssertion(() =>
        {
            var speaker = entMan.SpawnEntity("QueekishHumanDummy", MapCoordinates.Nullspace);
            metaData.SetEntityName(speaker, "John Smith");

            var ev = new AccentGetEvent(speaker, "I am leaving");
            entMan.EventBus.RaiseLocalEvent(speaker, ref ev);

            Assert.That(ev.Message, Is.EqualTo("I am leaving"),
                "The pronoun rewrite should only apply to Skaven speakers.");

            entMan.DeleteEntity(speaker);
        });

        await pair.CleanReturnAsync();
    }
}
