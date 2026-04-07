using Content.Server.Atmos;
using Content.Shared.Atmos;
using Robust.Shared.IoC;

namespace Content.Server.RussStation.Atmos;

/// <summary>
///     Helper for gas reactions to emit radiation pulses.
///     Reactions are data definitions without access to EntityManager,
///     so this resolves it via IoC and raises the broadcast event.
/// </summary>
public static class RadiationPulseHelper
{
    public static void EmitRadiation(IGasMixtureHolder? holder, float rads)
    {
        if (holder is not TileAtmosphere tile)
            return;

        if (rads <= 0f)
            return;

        var entMan = IoCManager.Resolve<IEntityManager>();
        var ev = new AtmosRadiationPulseEvent(tile.GridIndex, tile.GridIndices, rads);
        entMan.EventBus.RaiseEvent(EventSource.Local, ref ev);
    }
}
