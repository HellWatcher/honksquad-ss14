using Content.Client.UserInterface.Controls;
using Content.Shared.Administration;

namespace Content.Client.Administration;

/// <summary>
/// This handles the client portion of quick dialogs.
/// </summary>
public sealed class QuickDialogSystem : EntitySystem
{
    // HONK START - Track windows for server-side close
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
            RaiseNetworkEvent(new QuickDialogResponseEvent(ev.DialogId,
                responses,
                QuickDialogButtonFlag.OkButton));
            // HONK START
            _openWindows.Remove(ev.DialogId);
            // HONK END
        };

        window.OnCancelled += () =>
        {
            RaiseNetworkEvent(new QuickDialogResponseEvent(ev.DialogId,
                new(),
                QuickDialogButtonFlag.CancelButton));
            // HONK START
            _openWindows.Remove(ev.DialogId);
            // HONK END
        };
    }

    // HONK START - Server-side dialog close
    private void OnCloseDialog(QuickDialogCloseEvent ev)
    {
        if (!_openWindows.TryGetValue(ev.DialogId, out var window))
            return;

        _openWindows.Remove(ev.DialogId);

        // Detach handlers so closing doesn't send a cancel response back to the server
        // (the server already cleaned up this dialog).
        window.OnConfirmed = null;
        window.OnCancelled = null;
        window.Close();
    }
    // HONK END
}
