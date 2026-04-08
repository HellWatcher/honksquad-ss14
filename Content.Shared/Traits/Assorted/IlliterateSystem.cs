using Content.Shared.Paper;
using Content.Shared.Popups;
using Content.Shared.UserInterface;

namespace Content.Shared.Traits.Assorted;

/// <summary>
/// Prevents entities with <see cref="IlliterateComponent"/> from reading paper.
/// </summary>
public sealed class IlliterateSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<IlliterateComponent, UserOpenActivatableUIAttemptEvent>(OnOpenUIAttempt);
    }

    private void OnOpenUIAttempt(Entity<IlliterateComponent> ent, ref UserOpenActivatableUIAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!HasComp<PaperComponent>(args.Target))
            return;

        args.Cancel();

        if (!args.Silent)
            _popup.PopupClient(Loc.GetString("illiterate-cannot-read"), ent, ent);
    }
}
