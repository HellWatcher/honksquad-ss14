using Robust.Shared.Serialization;

namespace Content.Shared.RussStation.Economy.Events;

/// <summary>
/// A networked event raised when the server wants to close an open quick dialog.
/// </summary>
[Serializable, NetSerializable]
public sealed class QuickDialogCloseEvent : EntityEventArgs
{
    public int DialogId;

    public QuickDialogCloseEvent(int dialogId)
    {
        DialogId = dialogId;
    }
}
