using Content.Server.Chat.Systems;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Ghost;
using Content.Shared.RussStation.VerbBindings;
using Content.Shared.Speech;
using Content.Shared.Speech.Muting;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.RussStation.VerbBindings;

/// <summary>
/// HONK Grants one <c>HonkActionEmote</c> action entity per emote the player-controlled mob can
/// normally perform, and handles the action trigger by firing the emote through
/// <c>ChatSystem.TryEmoteWithChat</c>. The set comes from every <c>EmotePrototype</c> that passes
/// <c>SharedChatSystem.AllowedToUseEmote</c> for the performer, so species whitelists,
/// <c>SpeechComponent.AllowedEmotes</c>, and per-emote whitelist / blacklist all apply exactly as
/// they do when the player types the emote command. Emotes show up as regular action buttons in
/// the action menu and drag onto hotbar slots like any other action.
/// </summary>
public sealed class HonkEmoteActionSystem : EntitySystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;

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

        // Ghosts, lobby observers, and anything else without a SpeechComponent can't emote, so
        // granting them emote actions just floods the menu. A living mob with speech is the
        // thing we actually want these buttons on.
        if (HasComp<GhostComponent>(performer) || !HasComp<SpeechComponent>(performer))
            return;

        // Reconnect or re-attach to the same body: skip if this mob already carries any emote
        // action so we don't duplicate the full emote set in its menu every time.
        if (TryComp<Content.Shared.Actions.Components.ActionsComponent>(performer, out var actions))
        {
            foreach (var existing in actions.Actions)
            {
                if (HasComp<HonkEmoteActionComponent>(existing))
                    return;
            }
        }

        foreach (var emote in _proto.EnumeratePrototypes<EmotePrototype>())
        {
            // Invalid-category protos exist as bases; they aren't meant for players.
            if (emote.Category == EmoteCategory.Invalid)
                continue;

            // Same gate ChatSystem uses when a player types the emote command. Species
            // whitelists, per-emote whitelist / blacklist, and Available=false all apply here.
            if (!_chat.AllowedToUseEmote(performer, emote))
                continue;

            // Spawn, configure, then grant. Doing it in that order means the first network
            // state the client sees for the new action entity already has Emote populated;
            // otherwise the client's OnActionAdded fires before the delta from Dirty reaches
            // it and the placement-persistence lookup misses.
            EntityUid? actionId = null;
            if (!_actionContainer.EnsureAction(performer, ref actionId, out _, EmoteActionProtoId))
                continue;
            var actionUid = actionId.Value;

            if (TryComp<HonkEmoteActionComponent>(actionUid, out var tag))
            {
                tag.Emote = emote.ID;
                Dirty(actionUid, tag);
            }

            _actions.SetIcon(actionUid, emote.Icon);
            _meta.SetEntityName(actionUid, Loc.GetString(emote.Name));

            _actions.AddActionDirect(performer, actionUid);
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
