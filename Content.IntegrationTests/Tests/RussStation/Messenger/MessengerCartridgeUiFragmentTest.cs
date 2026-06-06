using System.Collections.Generic;
using Content.Client.RussStation.Messenger;
using Content.Shared.RussStation.Messenger;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;

namespace Content.IntegrationTests.Tests.RussStation.Messenger;

/// <summary>
/// Client-side regression coverage for <see cref="MessengerCartridgeUiFragment"/>. Drives the
/// public <c>UpdateState</c> entry point and asserts the one cleanly observable output, the header
/// label, across the contact-list / chat-view transitions. The chat-view cases in particular guard
/// the cached active-contact lookup: switching conversations must refresh the resolved name rather
/// than reuse the previous one.
/// </summary>
[TestFixture]
[TestOf(typeof(MessengerCartridgeUiFragment))]
public sealed class MessengerCartridgeUiFragmentTest
{
    private static MessengerUiState ContactListState(List<MessengerContact> contacts) =>
        new(contacts, activeConversation: null, messages: null, muted: false, hasId: true, address: "NT0001");

    private static MessengerUiState ChatViewState(List<MessengerContact> contacts, NetEntity active) =>
        new(contacts, active, new List<MessengerMessageEntry>(), muted: false, hasId: true, address: "NT0001");

    [Test]
    public async Task HeaderTracksActiveConversationTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var client = pair.Client;

        await client.WaitAssertion(() =>
        {
            var alice = new NetEntity(1);
            var bob = new NetEntity(2);
            var contacts = new List<MessengerContact>
            {
                new(alice, "Alice Smith", "Engineer", "JobIconEngineer"),
                new(bob, "Bob Jones", "Botanist", "JobIconBotanist"),
            };

            var fragment = new MessengerCartridgeUiFragment();
            var header = fragment.FindControl<Label>("HeaderLabel");

            // Contact list: header shows the program name.
            fragment.UpdateState(ContactListState(contacts));
            Assert.That(header.Text, Is.EqualTo(Loc.GetString("messenger-program-name")));

            // Opening a conversation resolves the contact's display name.
            fragment.UpdateState(ChatViewState(contacts, alice));
            Assert.That(header.Text, Is.EqualTo("Alice Smith"));

            // Switching conversations must refresh the cached name, not reuse Alice's.
            fragment.UpdateState(ChatViewState(contacts, bob));
            Assert.That(header.Text, Is.EqualTo("Bob Jones"),
                "Header should follow the new active conversation, proving the cache keys on the conversation.");

            // Returning to the contact list restores the program name.
            fragment.UpdateState(ContactListState(contacts));
            Assert.That(header.Text, Is.EqualTo(Loc.GetString("messenger-program-name")));

            // An active conversation with no matching contact (e.g. the cartridge reopened while a
            // conversation persisted) resolves to an empty name rather than a stale one.
            fragment.UpdateState(ChatViewState(contacts, new NetEntity(99)));
            Assert.That(header.Text, Is.EqualTo(string.Empty));

            fragment.Dispose();
        });

        await pair.CleanReturnAsync();
    }
}
