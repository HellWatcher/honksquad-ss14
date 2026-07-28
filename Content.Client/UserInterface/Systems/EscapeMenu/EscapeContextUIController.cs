using Content.Client.UserInterface.Systems.Info;
// HONK START - honksquad #513: Escape cancels active DoAfters before falling through to window/menu
using Content.Client.RussStation.DoAfterCancel;
// HONK END
using Content.Shared.Input;
using JetBrains.Annotations;
using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;

namespace Content.Client.UserInterface.Systems.EscapeMenu;

[UsedImplicitly]
public sealed partial class EscapeContextUIController : UIController
{
    [Dependency] private IInputManager _inputManager = default!;

    [Dependency] private CloseRecentWindowUIController _closeRecentWindowUIController = default!;
    [Dependency] private EscapeUIController _escapeUIController = default!;
    // HONK START - honksquad #513
    [Dependency] private IEntityManager _entityManager = default!;
    // HONK END

    public override void Initialize()
    {
        _inputManager.SetInputCommand(ContentKeyFunctions.EscapeContext,
            InputCmdHandler.FromDelegate(_ => CloseWindowOrOpenGameMenu()));
    }

    private void CloseWindowOrOpenGameMenu()
    {
        if (_closeRecentWindowUIController.HasClosableWindow())
        {
            _closeRecentWindowUIController.CloseMostRecentWindow();
        }
        else
        {
            // HONK START - honksquad #513: cancel active DoAfters before opening the escape menu
            if (_entityManager.System<DoAfterCancelRequestSystem>().TryRequestCancel())
                return;
            // HONK END
            _escapeUIController.ToggleWindow();
        }
    }
}
