using Robust.Shared.Serialization;

namespace Content.Shared.RussStation.DoAfterCancel;

/// <summary>
/// Sent client -> server when the local player pressed Escape with no UI
/// capturing input. The server cancels every active DoAfter where the
/// sender's attached entity is the DoAfter user; forced DoAfters where
/// the player is merely the target stay running.
/// </summary>
[Serializable, NetSerializable]
public sealed class CancelAllDoAftersEvent : EntityEventArgs;
