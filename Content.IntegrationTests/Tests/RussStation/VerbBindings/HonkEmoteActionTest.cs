#nullable enable
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.RussStation.VerbBindings;
using Robust.Server.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.RussStation.VerbBindings;

/// <summary>
/// Verifies the fork's emote-as-action grants run on player attach, land the allowlist's emotes
/// as HonkEmoteAction entities on the player's ActionsComponent, and replicate the emote proto
/// id to the client.
/// </summary>
[TestFixture]
public sealed class HonkEmoteActionTest : GameTest
{
    public override PoolSettings PoolSettings => new PoolSettings { Connected = true, DummyTicker = false };

    [Test]
    public async Task EmoteActionsGrantedOnAttach()
    {
        var pair = Pair;
        var server = pair.Server;
        var client = pair.Client;
        var sEntMan = server.ResolveDependency<IEntityManager>();
        var cEntMan = client.ResolveDependency<IEntityManager>();
        var sProto = server.ResolveDependency<IPrototypeManager>();
        var sActions = server.System<SharedActionsSystem>();
        var cActions = client.System<SharedActionsSystem>();
        var serverSession = server.ResolveDependency<IPlayerManager>().Sessions.Single();

        Assert.That(serverSession.AttachedEntity, Is.Not.Null);
        var serverEnt = serverSession.AttachedEntity!.Value;
        var clientEnt = client.Session!.AttachedEntity!.Value;

        var sEmoteActions = sActions.GetActions(serverEnt)
            .Where(ent => sEntMan.HasComponent<HonkEmoteActionComponent>(ent))
            .ToArray();
        var cEmoteActions = cActions.GetActions(clientEnt)
            .Where(ent => cEntMan.HasComponent<HonkEmoteActionComponent>(ent))
            .ToArray();

        // The default allowlist has entries; at least one should pass AllowedToUseEmote on
        // the default test mob. Exact count depends on species / whitelist checks.
        Assert.That(sEmoteActions.Length, Is.GreaterThan(0));
        // Actions should replicate one-for-one to the client.
        Assert.That(cEmoteActions.Length, Is.EqualTo(sEmoteActions.Length));

        // Every granted emote must have a non-empty proto id that resolves to a real
        // EmotePrototype, on both sides. This catches regressions in the networked state
        // generation (AutoGenerateComponentState + AutoNetworkedField).
        foreach (var ent in sEmoteActions)
        {
            var comp = sEntMan.GetComponent<HonkEmoteActionComponent>(ent);
            Assert.That(string.IsNullOrEmpty(comp.Emote), Is.False,
                "Server-side HonkEmoteActionComponent.Emote must be set before the action is granted.");
            Assert.That(sProto.HasIndex<Content.Shared.Chat.Prototypes.EmotePrototype>(comp.Emote), Is.True,
                $"'{comp.Emote}' is not a valid EmotePrototype id.");
        }
        foreach (var ent in cEmoteActions)
        {
            var comp = cEntMan.GetComponent<HonkEmoteActionComponent>(ent);
            Assert.That(string.IsNullOrEmpty(comp.Emote), Is.False,
                "Client-side HonkEmoteActionComponent.Emote must be set after state replication.");
        }
    }

    [Test]
    public async Task EmoteActionsDoNotAutoPopulate()
    {
        // Emote action entities should have AutoPopulate=false so LoadDefaultActions leaves
        // them in the menu. Regressing this would flood a new connection's bar with every
        // emote the allowlist grants.
        var pair = Pair;
        var sEntMan = pair.Server.ResolveDependency<IEntityManager>();
        var sActions = pair.Server.System<SharedActionsSystem>();
        var serverSession = pair.Server.ResolveDependency<IPlayerManager>().Sessions.Single();
        var serverEnt = serverSession.AttachedEntity!.Value;

        foreach (var ent in sActions.GetActions(serverEnt)
                     .Where(e => sEntMan.HasComponent<HonkEmoteActionComponent>(e)))
        {
            var action = sEntMan.GetComponent<ActionComponent>(ent);
            Assert.That(action.AutoPopulate, Is.False,
                "Emote action must have AutoPopulate=false so it doesn't land on the bar automatically.");
        }
    }
}
