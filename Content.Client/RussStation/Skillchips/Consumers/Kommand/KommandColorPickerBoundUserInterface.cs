using Content.Shared.RussStation.Skillchips.Consumers.Kommand;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.RussStation.Skillchips.Consumers.Kommand;

/// <summary>
/// BUI hosted on the chip-holder mob, opened by the chip's color-picker action
/// via the stock <c>OpenUiActionEvent</c> path. Renders an HSV/RGB slider
/// matching the accessibility menu's <c>ColorSelectorSliders</c>; each slider
/// move sends a <see cref="KommandSetColorBuiMessage"/> to the server which
/// updates <see cref="KommandColorPreferenceComponent.Color"/> on the mob.
/// </summary>
[UsedImplicitly]
public sealed class KommandColorPickerBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private KommandColorPickerWindow? _window;

    public KommandColorPickerBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<KommandColorPickerWindow>();
        _window.OnColorSelected += SendPick;

        // Seed the slider with the holder's current preference so the picker doesn't snap to white.
        if (EntMan.TryGetComponent<KommandColorPreferenceComponent>(Owner, out var pref))
            _window.SetColor(pref.Color);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_window == null)
            return;

        if (EntMan.TryGetComponent<KommandColorPreferenceComponent>(Owner, out var pref))
            _window.SetColor(pref.Color);
    }

    private void SendPick(Color color)
        => SendPredictedMessage(new KommandSetColorBuiMessage(color));
}
