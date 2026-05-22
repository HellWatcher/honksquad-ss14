using Content.Shared.Examine;
using Content.Shared.RussStation.Skillchips.Systems;

namespace Content.Shared.RussStation.Skillchips.Consumers;

/// <summary>
/// Examine hook for the 3NTR41LS skillchip (<c>entrails_reader</c> capability).
/// When a chip holder examines a tagged <see cref="EntrailsReadableComponent"/>
/// organ inside details range, append a chip-only flavor line. v1 is pure
/// scaffolding so the hook is in place; follow-ups can swap the line for real
/// reads once organ-side data (damage, metabolism, prior owner) lands.
/// Lives in shared so the client sees the line on its predicted examine.
/// </summary>
public sealed class SharedEntrailsReaderSystem : EntitySystem
{
    public const string EntrailsReaderTag = "entrails_reader";

    [Dependency] private readonly SharedSkillchipSystem _skillchip = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<EntrailsReadableComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<EntrailsReadableComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (!_skillchip.HasCapability(args.Examiner, EntrailsReaderTag))
            return;

        args.PushMarkup(Loc.GetString("skillchip-entrails-reader-examine"));
    }
}
