using Content.Shared.Body;
using Content.Shared.RussStation.Skillchips;
using Content.Shared.RussStation.Skillchips.Systems;

namespace Content.Shared.RussStation.Skillchips.Consumers.Kommand;

/// <summary>
/// Kommand chip lifecycle + color-picker BUI bookkeeping. Two responsibilities:
///
/// 1. When a brain with the <c>enhanced_pointing</c> capability is inserted
///    into a body, attach <see cref="KommandColorPreferenceComponent"/> to
///    the body and register the color-picker BUI on a runtime
///    <see cref="UserInterfaceComponent"/>. ActivatableUISystem then handles
///    the chip's action click via the stock <c>OpenUiActionEvent</c> path,
///    so this chip doesn't need its own action handler.
///
/// 2. Persist the chosen color from <see cref="KommandSetColorBuiMessage"/>.
///
/// The actual point-time arrow stamping lives in the server-only subclass
/// (Content.Server) because <c>AfterPointedAtEvent</c> is server-raised and
/// the live <c>PointingArrowComponent</c> is server-only.
/// </summary>
public abstract class SharedKommandSystem : EntitySystem
{
    public const string EnhancedPointingTag = "enhanced_pointing";

    /// <summary>Robust BUI lookup string for the client-side picker.</summary>
    protected const string KommandColorPickerBuiName = "KommandColorPickerBoundUserInterface";

    [Dependency] protected readonly SharedSkillchipSystem Skillchip = default!;
    [Dependency] protected readonly SharedUserInterfaceSystem Ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Body-side events: SharedSkillchipSystem hooks the organ-side OrganGotInsertedEvent
        // pair, and the engine forbids two systems subscribing to the same (component, event).
        // We hang off the body's OrganInsertedIntoEvent / OrganRemovedFromEvent instead, which
        // arrive on BodyComponent right after the chip system's organ-side handlers run.
        SubscribeLocalEvent<BodyComponent, OrganInsertedIntoEvent>(OnOrganInserted);
        SubscribeLocalEvent<BodyComponent, OrganRemovedFromEvent>(OnOrganRemoved);

        SubscribeLocalEvent<KommandColorPreferenceComponent, KommandSetColorBuiMessage>(OnSetColor);
    }

    private void OnOrganInserted(Entity<BodyComponent> body, ref OrganInsertedIntoEvent args)
    {
        // The inserted organ may be a brain (the only thing that carries skillchips); other
        // organs are noise and bail cheap on the HasComp check.
        if (!HasComp<SkillchipHolderComponent>(args.Organ))
            return;

        if (!Skillchip.BrainHasCapability(args.Organ, EnhancedPointingTag))
            return;

        AttachPreference(body.Owner);
    }

    private void OnOrganRemoved(Entity<BodyComponent> body, ref OrganRemovedFromEvent args)
    {
        if (!HasComp<SkillchipHolderComponent>(args.Organ))
            return;

        // Other organs in the same body could in theory carry the capability; keep the
        // preference if any holder still does, otherwise clean it off the body.
        if (Skillchip.HasCapability(body.Owner, EnhancedPointingTag))
            return;

        if (HasComp<KommandColorPreferenceComponent>(body.Owner))
            RemComp<KommandColorPreferenceComponent>(body.Owner);
    }

    /// <summary>
    /// EnsureComp the preference and the runtime UI registration. Idempotent so brain
    /// transplants between bodies don't accumulate state.
    /// </summary>
    protected void AttachPreference(EntityUid mob)
    {
        EnsureComp<KommandColorPreferenceComponent>(mob);

        var ui = EnsureComp<UserInterfaceComponent>(mob);
        Ui.SetUi((mob, ui), KommandUiKey.ColorPicker, new InterfaceData(KommandColorPickerBuiName));
    }

    private void OnSetColor(Entity<KommandColorPreferenceComponent> mob, ref KommandSetColorBuiMessage args)
    {
        if (mob.Comp.Color == args.Color)
            return;

        mob.Comp.Color = args.Color;
        Dirty(mob);
    }
}
