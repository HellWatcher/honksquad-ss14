using Content.Shared.RussStation.Botany;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.RussStation.Botany.UI;

[UsedImplicitly]
public sealed class PlantAnalyzerBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private PlantAnalyzerWindow? _window;

    public PlantAnalyzerBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<PlantAnalyzerWindow>();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_window == null)
            return;

        if (state is PlantAnalyzerScannedUserMessage msg)
            _window.Populate(msg);
    }
}
