using Robust.Shared.Serialization;

namespace Content.Shared.RussStation.Skillchips.Consumers.Kommand;

/// <summary>BUI key for the Kommand color picker, hosted on the chip-holder mob.</summary>
[Serializable, NetSerializable]
public enum KommandUiKey : byte
{
    ColorPicker,
}

/// <summary>Sent from the picker BUI to the server when the user selects a color.</summary>
[Serializable, NetSerializable]
public sealed class KommandSetColorBuiMessage(Color color) : BoundUserInterfaceMessage
{
    public Color Color = color;
}
