using Content.Shared.Atmos;
using Content.Shared.Atmos.Piping.Portable.Components;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.Atmos.UI;

[UsedImplicitly]
public sealed class PortableScrubberBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private PortableScrubberWindow? _window;

    public PortableScrubberBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<PortableScrubberWindow>();

        _window.OnGasToggled += gas =>
        {
            SendMessage(new PortableScrubberToggleFilterGasMessage(gas));
        };

        _window.ToggleStatusButton.OnToggled += _ =>
        {
            SendMessage(new PortableScrubberToggleEnabledMessage());
        };
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (_window == null || state is not PortableScrubberBoundUserInterfaceState cast)
            return;

        _window.SetActive(cast.Enabled);
        _window.UpdateState(cast);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;
        _window?.Dispose();
    }
}
