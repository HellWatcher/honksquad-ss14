using Content.Client.UserInterface.Controls;
using Content.Shared.Administration;

namespace Content.Client.Administration;

/// <summary>
/// This handles the client portion of quick dialogs.
/// </summary>
public sealed class QuickDialogSystem : EntitySystem
{
    // HONK START - Track open windows for server-initiated close (#163)
    private readonly Dictionary<int, DialogWindow> _openWindows = new();
    // HONK END

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeNetworkEvent<QuickDialogOpenEvent>(OpenDialog);
        // HONK START
        SubscribeNetworkEvent<QuickDialogCloseEvent>(OnCloseDialog);
        // HONK END
    }

    private void OpenDialog(QuickDialogOpenEvent ev)
    {
        var ok = (ev.Buttons & QuickDialogButtonFlag.OkButton) != 0;
        var cancel = (ev.Buttons & QuickDialogButtonFlag.CancelButton) != 0;
        var window = new DialogWindow(ev.Title, ev.Prompts, ok: ok, cancel: cancel);

        // HONK START
        _openWindows[ev.DialogId] = window;
        // HONK END

        window.OnConfirmed += responses =>
        {
            // HONK START
            _openWindows.Remove(ev.DialogId);
            // HONK END
            RaiseNetworkEvent(new QuickDialogResponseEvent(ev.DialogId,
                responses,
                QuickDialogButtonFlag.OkButton));
        };

        window.OnCancelled += () =>
        {
            // HONK START
            _openWindows.Remove(ev.DialogId);
            // HONK END
            RaiseNetworkEvent(new QuickDialogResponseEvent(ev.DialogId,
                new(),
                QuickDialogButtonFlag.CancelButton));
        };
    }

    // HONK START
    private void OnCloseDialog(QuickDialogCloseEvent ev)
    {
        if (_openWindows.Remove(ev.DialogId, out var window))
            window.Close();
    }
    // HONK END
}
