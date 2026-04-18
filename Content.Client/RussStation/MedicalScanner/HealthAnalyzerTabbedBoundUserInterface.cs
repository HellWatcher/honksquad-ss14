using System.Globalization;
using Content.Shared.MedicalScanner;
using Content.Shared.RussStation.MedicalScanner;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.RussStation.MedicalScanner;

[UsedImplicitly]
public sealed class HealthAnalyzerTabbedBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private HealthAnalyzerTabbedWindow? _window;

    public HealthAnalyzerTabbedBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<HealthAnalyzerTabbedWindow>();

        _window.Title = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(
            EntMan.GetComponent<MetaDataComponent>(Owner).EntityName);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_window is null)
            return;

        if (state is HealthAnalyzerReagentState reagentState)
        {
            _window.PopulateReagents(reagentState);
            if (reagentState.PreferredTab is { } tab)
                _window.SelectTab(tab);
        }
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        if (_window is null)
            return;

        if (message is HealthAnalyzerScannedUserMessage healthMsg)
            _window.PopulateHealth(healthMsg);
    }
}
