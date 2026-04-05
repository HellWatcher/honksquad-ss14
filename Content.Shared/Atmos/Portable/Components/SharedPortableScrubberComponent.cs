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

    public PortableScrubberBoundUserInterfaceState(HashSet<Gas> filterGases, bool enabled)
    {
        FilterGases = filterGases;
        Enabled = enabled;
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
