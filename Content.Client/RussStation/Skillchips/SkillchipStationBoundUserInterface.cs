using Content.Shared.RussStation.Skillchips;
using Robust.Client.UserInterface;

namespace Content.Client.RussStation.Skillchips;

public sealed class SkillchipStationBoundUserInterface : BoundUserInterface
{
    private SkillchipStationWindow? _window;

    public SkillchipStationBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<SkillchipStationWindow>();
        _window.OnImplant += () => SendMessage(new SkillchipStationImplantMessage());
        _window.OnEject += () => SendMessage(new SkillchipStationEjectMessage());
        _window.OnRemove += proto => SendMessage(new SkillchipStationRemoveMessage(proto));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (_window == null || state is not SkillchipStationBoundUserInterfaceState cast)
            return;

        _window.UpdateState(cast);
    }
}
