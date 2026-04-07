using Content.Server.Administration;
using Robust.Shared.Player;

namespace Content.Server.RussStation.UI;

/// <summary>
/// Wraps <see cref="QuickDialogSystem"/> with per-session tracking to prevent
/// stacking dialogs on repeated interactions. Systems inject this instead of
/// managing their own HashSet of pending sessions.
/// </summary>
public sealed class TrackedDialogSystem : EntitySystem
{
    [Dependency] private readonly QuickDialogSystem _quickDialog = default!;

    private readonly HashSet<ICommonSession> _pending = new();

    /// <summary>
    /// Opens a single-field tracked dialog. If the session already has a dialog
    /// open through this system, the call is silently ignored.
    /// </summary>
    public void OpenDialog<T>(ICommonSession session, string title, string prompt, Action<T> onConfirm)
    {
        if (!_pending.Add(session))
            return;

        _quickDialog.OpenDialog(session, title, prompt,
            (T value) =>
            {
                _pending.Remove(session);
                onConfirm(value);
            },
            () => _pending.Remove(session));
    }

    /// <summary>
    /// Returns true if the given session has a tracked dialog open.
    /// </summary>
    public bool HasPendingDialog(ICommonSession session)
    {
        return _pending.Contains(session);
    }

    /// <summary>
    /// Closes any tracked dialog for the session and cleans up tracking state.
    /// Call this when the item is dropped or the interaction is otherwise interrupted.
    /// </summary>
    public void CancelDialog(ICommonSession session)
    {
        if (!_pending.Remove(session))
            return;

        _quickDialog.CloseAllDialogs(session);
    }
}
