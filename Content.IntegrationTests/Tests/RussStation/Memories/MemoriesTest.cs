using Content.Server.RussStation.Memories;
using Content.Shared.RussStation.Memories;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.RussStation.Memories;

/// <summary>
/// Verifies <see cref="MemoriesSystem"/> key-value storage: adding a memory creates the
/// component and stores the value, removing reports whether the key existed, and multiple keys
/// coexist independently.
/// </summary>
[TestFixture]
[TestOf(typeof(MemoriesSystem))]
public sealed class MemoriesTest
{
    [Test]
    public async Task AddAndRemoveMemoriesTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var memories = entMan.System<MemoriesSystem>();

        await server.WaitAssertion(() =>
        {
            var mob = entMan.SpawnEntity(null, MapCoordinates.Nullspace);

            // Adding a memory creates the component on demand.
            Assert.That(entMan.HasComponent<MemoriesComponent>(mob), Is.False);
            memories.AddMemory(mob, "code", "1234");
            Assert.That(entMan.HasComponent<MemoriesComponent>(mob), Is.True);

            var comp = entMan.GetComponent<MemoriesComponent>(mob);
            Assert.That(comp.Memories, Does.ContainKey("code"));
            Assert.That(comp.Memories["code"], Is.EqualTo("1234"));

            // A second key coexists with the first.
            memories.AddMemory(mob, "secret", "honk");
            Assert.That(comp.Memories, Has.Count.EqualTo(2));
            Assert.That(comp.Memories["secret"], Is.EqualTo("honk"));

            // Overwriting an existing key replaces the value.
            memories.AddMemory(mob, "code", "5678");
            Assert.That(comp.Memories["code"], Is.EqualTo("5678"));
            Assert.That(comp.Memories, Has.Count.EqualTo(2));

            // Removing an existing key succeeds; removing it again fails.
            Assert.That(memories.RemoveMemory(mob, "code"), Is.True);
            Assert.That(comp.Memories, Does.Not.ContainKey("code"));
            Assert.That(memories.RemoveMemory(mob, "code"), Is.False);

            entMan.DeleteEntity(mob);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RemoveFromEntityWithoutComponentReturnsFalseTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var memories = entMan.System<MemoriesSystem>();

        await server.WaitAssertion(() =>
        {
            var mob = entMan.SpawnEntity(null, MapCoordinates.Nullspace);

            Assert.That(memories.RemoveMemory(mob, "nothing"), Is.False,
                "Removing from an entity with no MemoriesComponent should report false.");

            entMan.DeleteEntity(mob);
        });

        await pair.CleanReturnAsync();
    }
}
