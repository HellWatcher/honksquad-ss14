using Content.Shared.RussStation.MedicalScanner;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.RussStation.MedicalScanner;

[UsedImplicitly]
public sealed class HealthAnalyzerReagentBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private HealthAnalyzerReagentWindow? _window;

    public HealthAnalyzerReagentBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<HealthAnalyzerReagentWindow>();
        _window.Title = Loc.GetString("health-analyzer-reagent-window-title");
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        if (_window == null)
            return;

        if (message is not HealthAnalyzerReagentScannedMessage cast)
            return;

        _window.Populate(cast.State);
    }
}
