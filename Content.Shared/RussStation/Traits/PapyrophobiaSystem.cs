using Content.Shared.Interaction.Events;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Paper;
using Content.Shared.Popups;
using Content.Shared.UserInterface;

namespace Content.Shared.RussStation.Traits;

public sealed class PapyrophobiaSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PapyrophobiaComponent, UserOpenActivatableUIAttemptEvent>(OnOpenUIAttempt);
        SubscribeLocalEvent<PapyrophobiaComponent, PaperWriteAttemptEvent>(OnWriteAttempt);
        SubscribeLocalEvent<PaperComponent, UseInHandEvent>(OnPaperUseInHand, before: [typeof(IngestionSystem)]);
    }

    private void OnOpenUIAttempt(Entity<PapyrophobiaComponent> ent, ref UserOpenActivatableUIAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!HasComp<PaperComponent>(args.Target))
            return;

        args.Cancel();

        if (!args.Silent)
            _popup.PopupClient(Loc.GetString("papyrophobia-popup"), ent, ent);
    }

    private void OnWriteAttempt(Entity<PapyrophobiaComponent> ent, ref PaperWriteAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        args.FailReason = "papyrophobia-popup";
        args.Cancelled = true;
    }

    // Paper has both ActivatableUI and Edible. When the UI open is cancelled, ActivatableUISystem
    // leaves args.Handled = false, and IngestionSystem (which subscribes after it) then eats the
    // paper. Short-circuit UseInHandEvent before IngestionSystem runs.
    private void OnPaperUseInHand(Entity<PaperComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (!HasComp<PapyrophobiaComponent>(args.User))
            return;

        args.Handled = true;
        _popup.PopupClient(Loc.GetString("papyrophobia-popup"), args.User, args.User);
    }
}
