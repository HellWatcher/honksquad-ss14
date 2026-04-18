using Content.Shared.Atmos;
using Robust.Shared.Serialization;

namespace Content.Shared.Atmos.Piping.Portable.Components;

[Serializable, NetSerializable]
public enum PortableScrubberUiKey
{
    Key
}

[Serializable, NetSerializable]
public sealed class PortableScrubberBoundUserInterfaceState : BoundUserInterfaceState
{
    public HashSet<Gas> FilterGases { get; }
    public bool Enabled { get; }
    public bool Anchored { get; }
    public float Pressure { get; }
    public float MaxPressure { get; }

    public PortableScrubberBoundUserInterfaceState(HashSet<Gas> filterGases, bool enabled, bool anchored, float pressure, float maxPressure)
    {
        FilterGases = filterGases;
        Enabled = enabled;
        Anchored = anchored;
        Pressure = pressure;
        MaxPressure = maxPressure;
    }
}

[Serializable, NetSerializable]
public sealed class PortableScrubberToggleFilterGasMessage : BoundUserInterfaceMessage
{
    public Gas Gas { get; }

    public PortableScrubberToggleFilterGasMessage(Gas gas)
    {
        Gas = gas;
    }
}

[Serializable, NetSerializable]
public sealed class PortableScrubberToggleEnabledMessage : BoundUserInterfaceMessage
{
}
