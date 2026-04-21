using Robust.Shared.Serialization;

namespace Content.Shared.RussStation.Popups;

/// <summary>
/// Classifies a popup so the client can route it to the floating display, the popup log, or main chat.
/// Callers pick the category closest to the popup's meaning; Flavor is the default for uncategorized popups.
/// </summary>
[Serializable, NetSerializable]
public enum PopupCategory : byte
{
    /// <summary>
    /// Ambient, cosmetic, or uncategorized. Default when a caller doesn't specify.
    /// </summary>
    Flavor = 0,

    /// <summary>
    /// Damage taken or dealt, miss/dodge notifications, stamina crits.
    /// </summary>
    Combat,

    /// <summary>
    /// Bleeding, pain, status effect onset/offset, medical alerts on self.
    /// </summary>
    Medical,

    /// <summary>
    /// Temperature extremes, low pressure, slips, shocks, gas exposure.
    /// </summary>
    Environmental,

    /// <summary>
    /// "You pick up X", "you can't reach that", action results, tool use feedback.
    /// </summary>
    Interaction,

    /// <summary>
    /// Admin messages and error-like popups ("nothing happens", "something is wrong").
    /// </summary>
    System,
}
