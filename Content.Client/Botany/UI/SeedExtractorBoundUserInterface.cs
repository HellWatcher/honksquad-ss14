using Content.Shared.Botany;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.Botany.UI;

[UsedImplicitly]
public sealed class SeedExtractorBoundUserInterface : BoundUserInterface
{
    private SeedExtractorMenu? _menu;

    public SeedExtractorBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindowCenteredRight<SeedExtractorMenu>();
        _menu.OnTakePressed += groupKey =>
        {
            SendMessage(new SeedExtractorTakeSeedMessage(groupKey));
        };
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is SeedExtractorUpdateState seedState)
            _menu?.Populate(seedState.Seeds);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _menu?.Dispose();
    }
}
