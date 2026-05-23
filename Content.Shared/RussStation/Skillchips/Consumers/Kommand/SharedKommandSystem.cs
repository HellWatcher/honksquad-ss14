using Content.Shared.Actions.Components;
using Content.Shared.RussStation.Skillchips.Systems;
using Content.Shared.UserInterface;

namespace Content.Shared.RussStation.Skillchips.Consumers.Kommand;

/// <summary>
/// Kommand chip lifecycle + color-picker BUI bookkeeping. Two responsibilities:
///
/// 1. Catch the first click of the chip's color-picker action via the stock
///    <c>OpenUiActionEvent</c>, lazily EnsureComp
///    <see cref="KommandColorPreferenceComponent"/> on the mob plus a runtime
///    <see cref="UserInterfaceComponent"/> with the picker BUI key, then open
///    the UI ourselves and mark the event handled. (We can't hook
///    chip-install directly without modifying the chip system: the
///    OrganGotInsertedEvent the chip system uses for grant application only
///    fires on brain transplants, not on chip-into-existing-brain installs,
///    and the engine forbids two systems subscribing to the same
///    component+event pair anyway.)
///
/// 2. Persist the chosen color from <see cref="KommandSetColorBuiMessage"/>.
///
/// The actual point-time arrow stamping lives in the server-only subclass
/// (Content.Server) because the live <c>PointingArrowComponent</c> is
/// server-only.
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

        // ActionsComponent rides every mob that has actions, including this chip's action.
        // The event is dispatched broadcast on the performer with target=performer, so
        // anchoring on ActionsComponent catches it. We mark args.Handled ourselves and open
        // the UI directly, which keeps ActivatableUISystem's UserInterfaceComponent handler
        // out of the picture (the mob may not have UserInterfaceComponent until we add it).
        SubscribeLocalEvent<ActionsComponent, OpenUiActionEvent>(OnOpenPickerAction);
        SubscribeLocalEvent<KommandColorPreferenceComponent, KommandSetColorBuiMessage>(OnSetColor);
    }

    private void OnOpenPickerAction(Entity<ActionsComponent> mob, ref OpenUiActionEvent args)
    {
        if (args.Handled)
            return;

        if (args.Key is not KommandUiKey.ColorPicker)
            return;

        // Only chip holders should react to this key, even though the action grant should
        // already gate that. Belt-and-suspenders against a stray action proto reuse.
        if (!Skillchip.HasCapability(mob.Owner, EnhancedPointingTag))
            return;

        AttachPreference(mob.Owner);
        args.Handled = Ui.TryOpenUi(mob.Owner, KommandUiKey.ColorPicker, mob.Owner);
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
