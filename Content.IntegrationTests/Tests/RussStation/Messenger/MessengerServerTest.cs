using System.Linq;
using Content.Server.RussStation.Messenger;
using Content.Shared.PDA;
using Content.Shared.RussStation.Messenger;
using Content.Shared.StationRecords;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.RussStation.Messenger;

/// <summary>
/// Exercises the round-scoped message store in <see cref="MessengerServerSystem"/>: sending and
/// retrieving messages (with per-viewer "from self" framing), the ID-card write gate, unread
/// tracking, the per-conversation FIFO cap, message-length truncation, and the read-only
/// classification of antag / non-roster cartridges.
/// </summary>
[TestFixture]
[TestOf(typeof(MessengerServerSystem))]
public sealed class MessengerServerTest
{
    /// <summary>
    /// Build a cartridge wired into a PDA the way the loader does at runtime: the cartridge's
    /// transform parent is a PDA, and that PDA optionally holds an ID card.
    /// </summary>
    private static EntityUid MakeWiredCartridge(
        IEntityManager entMan,
        SharedTransformSystem xformSys,
        MapCoordinates coords,
        string address,
        bool withId = true,
        bool stationCrewId = false)
    {
        var pda = entMan.SpawnEntity(null, coords);
        var pdaComp = entMan.AddComponent<PdaComponent>(pda);

        if (withId)
        {
            var id = entMan.SpawnEntity(null, coords);
            if (stationCrewId)
            {
                var keyStorage = entMan.AddComponent<StationRecordKeyStorageComponent>(id);
                keyStorage.Key = new StationRecordKey(1, pda);
            }

            pdaComp.ContainedId = id;
        }

        var cart = entMan.SpawnEntity(null, coords);
        var cartComp = entMan.AddComponent<MessengerCartridgeComponent>(cart);
        cartComp.Address = address;
        xformSys.SetParent(cart, pda);

        return cart;
    }

    [Test]
    public async Task SendStoresMessageWithViewerPerspectiveTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = entMan.System<SharedMapSystem>();
        var xformSys = entMan.System<SharedTransformSystem>();
        var messenger = entMan.System<MessengerServerSystem>();

        await server.WaitAssertion(() =>
        {
            mapSys.CreateMap(out var mapId);
            var coords = new MapCoordinates(0, 0, mapId);

            var sender = MakeWiredCartridge(entMan, xformSys, coords, "NT0001");
            var recipient = MakeWiredCartridge(entMan, xformSys, coords, "NT0002");

            Assert.That(messenger.SendMessage(sender, recipient, "hello there"), Is.True);

            var fromSender = messenger.GetConversation(sender, recipient);
            Assert.That(fromSender, Has.Count.EqualTo(1));
            Assert.That(fromSender[0].Text, Is.EqualTo("hello there"));
            Assert.That(fromSender[0].SenderName, Is.EqualTo("NT0001"));
            Assert.That(fromSender[0].FromSelf, Is.True, "Sender should see their own message as from-self.");

            var fromRecipient = messenger.GetConversation(recipient, sender);
            Assert.That(fromRecipient, Has.Count.EqualTo(1));
            Assert.That(fromRecipient[0].FromSelf, Is.False, "Recipient should not see the message as from-self.");

            entMan.DeleteEntity(mapSys.GetMap(mapId));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SendRequiresIdCardTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = entMan.System<SharedMapSystem>();
        var xformSys = entMan.System<SharedTransformSystem>();
        var messenger = entMan.System<MessengerServerSystem>();

        await server.WaitAssertion(() =>
        {
            mapSys.CreateMap(out var mapId);
            var coords = new MapCoordinates(0, 0, mapId);

            var sender = MakeWiredCartridge(entMan, xformSys, coords, "NT0001", withId: false);
            var recipient = MakeWiredCartridge(entMan, xformSys, coords, "NT0002");

            Assert.That(messenger.SendMessage(sender, recipient, "hello"), Is.False,
                "Sending without an ID card in the PDA should be rejected.");
            Assert.That(messenger.GetConversation(sender, recipient), Is.Empty);

            entMan.DeleteEntity(mapSys.GetMap(mapId));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SendRejectsEmptyTextTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = entMan.System<SharedMapSystem>();
        var xformSys = entMan.System<SharedTransformSystem>();
        var messenger = entMan.System<MessengerServerSystem>();

        await server.WaitAssertion(() =>
        {
            mapSys.CreateMap(out var mapId);
            var coords = new MapCoordinates(0, 0, mapId);

            var sender = MakeWiredCartridge(entMan, xformSys, coords, "NT0001");
            var recipient = entMan.SpawnEntity(null, coords);

            Assert.That(messenger.SendMessage(sender, recipient, ""), Is.False);
            Assert.That(messenger.SendMessage(sender, recipient, "   "), Is.False);
            Assert.That(messenger.GetConversation(sender, recipient), Is.Empty);

            entMan.DeleteEntity(mapSys.GetMap(mapId));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task UnreadTrackingTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = entMan.System<SharedMapSystem>();
        var xformSys = entMan.System<SharedTransformSystem>();
        var messenger = entMan.System<MessengerServerSystem>();

        await server.WaitAssertion(() =>
        {
            mapSys.CreateMap(out var mapId);
            var coords = new MapCoordinates(0, 0, mapId);

            var sender = MakeWiredCartridge(entMan, xformSys, coords, "NT0001");
            var recipient = entMan.SpawnEntity(null, coords);

            Assert.That(messenger.HasUnread(recipient, sender), Is.False, "No messages yet, nothing unread.");

            messenger.SendMessage(sender, recipient, "ping");
            Assert.That(messenger.HasUnread(recipient, sender), Is.True, "A new message should be unread.");

            messenger.MarkRead(recipient, sender);
            Assert.That(messenger.HasUnread(recipient, sender), Is.False, "Marking read should clear unread.");

            messenger.SendMessage(sender, recipient, "ping again");
            Assert.That(messenger.HasUnread(recipient, sender), Is.True, "A later message should be unread again.");

            entMan.DeleteEntity(mapSys.GetMap(mapId));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ConversationCapTrimsOldestTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = entMan.System<SharedMapSystem>();
        var xformSys = entMan.System<SharedTransformSystem>();
        var messenger = entMan.System<MessengerServerSystem>();

        await server.WaitAssertion(() =>
        {
            mapSys.CreateMap(out var mapId);
            var coords = new MapCoordinates(0, 0, mapId);

            var sender = MakeWiredCartridge(entMan, xformSys, coords, "NT0001");
            var recipient = entMan.SpawnEntity(null, coords);

            var overflow = MessengerServerSystem.MaxMessagesPerConversation + 5;
            for (var i = 0; i < overflow; i++)
                messenger.SendMessage(sender, recipient, $"msg{i}");

            var conversation = messenger.GetConversation(sender, recipient);
            Assert.That(conversation, Has.Count.EqualTo(MessengerServerSystem.MaxMessagesPerConversation),
                "Conversation should be capped at MaxMessagesPerConversation.");
            Assert.That(conversation[0].Text, Is.EqualTo("msg5"),
                "The five oldest messages should have been trimmed off the front.");
            Assert.That(conversation.Last().Text, Is.EqualTo($"msg{overflow - 1}"),
                "The newest message should be retained.");

            entMan.DeleteEntity(mapSys.GetMap(mapId));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LongMessageTruncatedTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = entMan.System<SharedMapSystem>();
        var xformSys = entMan.System<SharedTransformSystem>();
        var messenger = entMan.System<MessengerServerSystem>();

        await server.WaitAssertion(() =>
        {
            mapSys.CreateMap(out var mapId);
            var coords = new MapCoordinates(0, 0, mapId);

            var sender = MakeWiredCartridge(entMan, xformSys, coords, "NT0001");
            var recipient = entMan.SpawnEntity(null, coords);

            var longText = new string('a', MessengerServerSystem.MaxMessageLength + 50);
            Assert.That(messenger.SendMessage(sender, recipient, longText), Is.True);

            var conversation = messenger.GetConversation(sender, recipient);
            Assert.That(conversation, Has.Count.EqualTo(1));
            Assert.That(conversation[0].Text, Has.Length.EqualTo(MessengerServerSystem.MaxMessageLength),
                "Over-length messages should be truncated to MaxMessageLength.");

            entMan.DeleteEntity(mapSys.GetMap(mapId));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ContactReadOnlyClassificationTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = entMan.System<SharedMapSystem>();
        var xformSys = entMan.System<SharedTransformSystem>();
        var messenger = entMan.System<MessengerServerSystem>();

        await server.WaitAssertion(() =>
        {
            mapSys.CreateMap(out var mapId);
            var coords = new MapCoordinates(0, 0, mapId);

            // Antag address: read-only regardless of ID.
            var antag = MakeWiredCartridge(entMan, xformSys, coords, "SY1234");
            Assert.That(messenger.IsContactReadOnly(antag), Is.True, "Antag-prefixed cartridges are read-only.");

            // Crew address but the ID is not on the station roster: still read-only.
            var unregistered = MakeWiredCartridge(entMan, xformSys, coords, "NT5678");
            Assert.That(messenger.IsContactReadOnly(unregistered), Is.True,
                "A crew address with no station record key is read-only (e.g. CentComm/ERT).");

            // Crew address with a station-record-keyed ID: writable.
            var crew = MakeWiredCartridge(entMan, xformSys, coords, "NT9012", stationCrewId: true);
            Assert.That(messenger.IsContactReadOnly(crew), Is.False,
                "A station-roster crew cartridge should be writable.");

            entMan.DeleteEntity(mapSys.GetMap(mapId));
        });

        await pair.CleanReturnAsync();
    }
}
