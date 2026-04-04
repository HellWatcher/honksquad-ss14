using Content.Shared.CartridgeLoader;
using Robust.Shared.Serialization;

namespace Content.Shared.RussStation.Economy;

[Serializable, NetSerializable]
public sealed class TogglePaycheckMuteMessage : CartridgeMessageEvent;
