using Content.Shared.Interaction.Events;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Paper;
using Content.Shared.Popups;
using Content.Shared.UserInterface;

namespace Content.Shared.Traits.Assorted;

/// <summary>
/// Prevents entities with <see cref="IlliterateComponent"/> from reading or writing on paper.
/// </summary>
public sealed class IlliterateSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<IlliterateComponent, UserOpenActivatableUIAttemptEvent>(OnOpenUIAttempt);
        SubscribeLocalEvent<IlliterateComponent, PaperWriteAttemptEvent>(OnWriteAttempt);
        SubscribeLocalEvent<PaperComponent, UseInHandEvent>(OnPaperUseInHand, before: [typeof(IngestionSystem)]);
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

    private void OnWriteAttempt(Entity<IlliterateComponent> ent, ref PaperWriteAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        args.FailReason = "illiterate-cannot-read";
        args.Cancelled = true;
    }

    // Paper has both ActivatableUI and Edible. When reading is cancelled via UserOpenActivatableUIAttemptEvent,
    // ActivatableUISystem leaves args.Handled = false, and IngestionSystem (which subscribes after it) then
    // eats the paper. Short-circuit UseInHandEvent here to prevent that fall-through.
    private void OnPaperUseInHand(Entity<PaperComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (!HasComp<IlliterateComponent>(args.User))
            return;

        args.Handled = true;
        _popup.PopupClient(Loc.GetString("illiterate-cannot-read"), args.User, args.User);
    }
}
