using Content.Shared.Examine;
using Content.Shared.RussStation.Skillchips.Systems;

namespace Content.Shared.RussStation.Skillchips.Consumers;

/// <summary>
/// Examine hook for the K33P-TH4T-D15K skillchip (<c>disk_verifier</c> capability).
/// When a chip holder examines a tagged <see cref="NukeDiskVerifiableComponent"/>
/// entity inside details range, append a warning line if the disk is a forgery.
/// Genuine disks get no line (silence is the "all good" signal), matching SS13's
/// <c>/obj/item/disk/nuclear/examine</c> behaviour on <c>TRAIT_DISK_VERIFIER</c>.
/// Lives in shared so the client also sees the warning on its predicted examine.
/// </summary>
public sealed class SharedDiskVerifierSystem : EntitySystem
{
    public const string DiskVerifierTag = "disk_verifier";

    [Dependency] private readonly SharedSkillchipSystem _skillchip = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NukeDiskVerifiableComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<NukeDiskVerifiableComponent> ent, ref ExaminedEvent args)
    {
        if (!ent.Comp.IsForgery)
            return;

        if (!args.IsInDetailsRange)
            return;

        if (!_skillchip.HasCapability(args.Examiner, DiskVerifierTag))
            return;

        args.PushMarkup(Loc.GetString("skillchip-disk-verifier-forgery"));
    }
}
