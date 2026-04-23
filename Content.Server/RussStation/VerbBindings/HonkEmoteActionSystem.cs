using Content.Server.Chat.Systems;
using Content.Shared.Actions;
using Content.Shared.Chat.Prototypes;
using Content.Shared.RussStation.VerbBindings;
using Content.Shared.Speech.Muting;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.RussStation.VerbBindings;

/// <summary>
/// HONK Grants one <c>HonkActionEmote</c> action entity per allowlisted emote to every player-controlled
/// mob when it's attached, and handles the action trigger by firing the emote through
/// <c>ChatSystem.TryEmoteWithChat</c>. The result is that emotes show up as regular action buttons
/// in the player's action menu and drag onto hotbar slots like any other action.
/// </summary>
public sealed class HonkEmoteActionSystem : EntitySystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;

    // Proto id of the allowlist prototype instance shipped by the fork.
    private const string AllowlistProtoId = "HonkDefault";

    // Action entity prototype spawned once per allowed emote.
    private const string EmoteActionProtoId = "HonkActionEmote";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<HonkEmoteActionComponent, HonkEmoteActionEvent>(OnActionFired);
    }

    private void OnPlayerAttached(PlayerAttachedEvent args)
    {
        var performer = args.Entity;
        if (!_proto.TryIndex<HonkEmoteActionAllowlistPrototype>(AllowlistProtoId, out var allowlist))
            return;

        foreach (var emoteId in allowlist.Emotes)
        {
            if (!_proto.TryIndex<EmotePrototype>(emoteId, out var emote))
                continue;

            // Skip emotes this specific mob can't perform (species whitelist, blacklist,
            // or Available=false and not in SpeechComponent.AllowedEmotes). Matches the
            // same gate ChatSystem uses when a player types the emote command.
            if (!_chat.AllowedToUseEmote(performer, emote))
                continue;

            var actionEntity = _actions.AddAction(performer, EmoteActionProtoId);
            if (actionEntity is not { } actionUid)
                continue;

            if (TryComp<HonkEmoteActionComponent>(actionUid, out var tag))
                tag.Emote = emoteId;

            _actions.SetIcon(actionUid, emote.Icon);
            _meta.SetEntityName(actionUid, Loc.GetString(emote.Name));
        }
    }

    private void OnActionFired(Entity<HonkEmoteActionComponent> ent, ref HonkEmoteActionEvent args)
    {
        if (args.Handled)
            return;
        if (HasComp<MutedComponent>(args.Performer))
            return;

        _chat.TryEmoteWithChat(args.Performer, ent.Comp.Emote);
        args.Handled = true;
    }
}
