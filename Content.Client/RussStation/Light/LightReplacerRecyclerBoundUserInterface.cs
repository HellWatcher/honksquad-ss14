using Content.Shared.RussStation.Light;
using Robust.Client.UserInterface;

namespace Content.Client.RussStation.Light;

public sealed class LightReplacerRecyclerBoundUserInterface : BoundUserInterface
{
    private LightReplacerRecyclerWindow? _window;

    public LightReplacerRecyclerBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<LightReplacerRecyclerWindow>();
        _window.OnExtract += proto => SendMessage(new LightReplacerExtractMessage(proto));
        _window.OnPrint += proto => SendMessage(new LightReplacerPrintMessage(proto));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (_window == null || state is not LightReplacerRecyclerBoundUserInterfaceState cast)
            return;

        _window.UpdateState(cast);
    }
}
