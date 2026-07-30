using Content.Server.Chat.Systems;
using Content.Shared.Chat;
using Content.Shared.Humanoid;
using Content.Shared.RussStation.Skillchips.Consumers;
using Content.Shared.RussStation.Skillchips.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.RussStation.Skillchips.Consumers;

/// <summary>
/// Cleanbot consumer for the <c>cleanbot_whisperer</c> capability tag.
/// Periodically picks a nearby chip-holder and emits a flavor emote, mirroring
/// SS13's <c>befriend_janitors</c> AI subtree. SS14 cleanbots have no hostile
/// mode wired up, so the SS13 faction-add half is purely flavor here too.
/// </summary>
public sealed partial class CleanbotWhispererSystem : EntitySystem
{
    public const string CleanbotWhispererTag = "cleanbot_whisperer";

    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedSkillchipSystem _skillchip = default!;

    private static readonly string[] CommonGreetings =
    {
        "skillchip-cleanbot-whisperer-greet-1",
        "skillchip-cleanbot-whisperer-greet-2",
        "skillchip-cleanbot-whisperer-greet-3",
        "skillchip-cleanbot-whisperer-greet-4",
        "skillchip-cleanbot-whisperer-greet-5",
    };

    private const string RareGreeting = "skillchip-cleanbot-whisperer-greet-rare";

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CleanbotWhispererBotComponent>();
        while (query.MoveNext(out var bot, out var comp))
        {
            if (now < comp.NextGreeting)
                continue;

            comp.NextGreeting = now + comp.Interval;
            TryGreetNearbyHolder(bot, comp);
        }
    }

    private void TryGreetNearbyHolder(EntityUid bot, CleanbotWhispererBotComponent comp)
    {
        var holderPresent = false;
        foreach (var humanoid in _lookup.GetEntitiesInRange<HumanoidProfileComponent>(Transform(bot).Coordinates, comp.Range))
        {
            if (_skillchip.HasCapability(humanoid.Owner, CleanbotWhispererTag))
            {
                holderPresent = true;
                break;
            }
        }

        if (!holderPresent)
            return;

        var rare = comp.RareOneIn > 0 && _random.Next(comp.RareOneIn) == 0;
        var line = Loc.GetString(rare ? RareGreeting : _random.Pick(CommonGreetings));

        _chat.TrySendInGameICMessage(bot, line, InGameICChatType.Emote, hideChat: false, hideLog: true);
    }
}
