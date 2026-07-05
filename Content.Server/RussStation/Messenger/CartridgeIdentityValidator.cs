using System.Diagnostics.CodeAnalysis;
using Content.Shared.Access.Components;
using Content.Shared.PDA;
using Content.Shared.RussStation.Messenger;
using Content.Shared.StationRecords;

namespace Content.Server.RussStation.Messenger;

/// <summary>
/// Consolidates the cartridge / ID-card predicates the messenger uses to decide who counts as
/// writable station crew and to resolve sender names. Wraps an <see cref="IEntityManager"/> and an
/// <see cref="AntagAddressFilter"/> so the chained component lookups live in one place.
/// </summary>
public sealed class CartridgeIdentityValidator
{
    private readonly IEntityManager _entMan;
    private readonly AntagAddressFilter _antagFilter;

    public CartridgeIdentityValidator(IEntityManager entMan, AntagAddressFilter antagFilter)
    {
        _entMan = entMan;
        _antagFilter = antagFilter;
    }

    /// <summary>
    /// Resolve the PDA a cartridge is loaded into (its transform parent), or false if that parent
    /// isn't a PDA.
    /// </summary>
    public bool TryGetPda(EntityUid cartUid, out EntityUid loaderUid, [NotNullWhen(true)] out PdaComponent? pda)
    {
        loaderUid = _entMan.GetComponent<TransformComponent>(cartUid).ParentUid;
        return _entMan.TryGetComponent(loaderUid, out pda);
    }

    /// <summary>
    /// Check if the cartridge's PDA has an ID card inserted.
    /// </summary>
    public bool HasIdCard(EntityUid cartUid)
    {
        return TryGetPda(cartUid, out _, out var pda) && pda.ContainedId != null;
    }

    /// <summary>
    /// Get the ID card name for a cartridge's PDA, or null if no ID is inserted. Falls back to the
    /// cartridge address when the inserted ID has no name.
    /// </summary>
    public string? GetCartridgeIdName(EntityUid cartUid)
    {
        if (!TryGetPda(cartUid, out _, out var pda) || pda.ContainedId == null)
            return null;

        if (_entMan.TryGetComponent<IdCardComponent>(pda.ContainedId.Value, out var idCard) &&
            !string.IsNullOrEmpty(idCard.FullName))
        {
            return idCard.FullName;
        }

        return _entMan.GetComponentOrNull<MessengerCartridgeComponent>(cartUid)?.Address ?? "?";
    }

    /// <summary>
    /// True if the ID card was issued through station records (i.e. the holder is on the station
    /// crew roster). CentComm/ERT IDs spawn directly from prototypes and lack a station record key.
    /// </summary>
    public bool IsStationCrewId(EntityUid idCard)
    {
        return _entMan.TryGetComponent<StationRecordKeyStorageComponent>(idCard, out var keyStorage)
               && keyStorage.Key != null;
    }

    /// <summary>
    /// Station crew = not an antag address, holds an ID, and that ID is registered in station
    /// records. Overload for when the address and PDA have already been resolved.
    /// </summary>
    public bool IsStationCrew(string address, PdaComponent pda)
    {
        return !_antagFilter.IsAntagAddress(address)
               && pda.ContainedId is { } id
               && IsStationCrewId(id);
    }

    /// <summary>
    /// True only when the cartridge belongs to station crew: not an antag address, has an ID
    /// inserted, and that ID is on the station records roster (so CentComm and ERT IDs are excluded).
    /// </summary>
    public bool IsStationCrewCartridge(EntityUid cartUid)
    {
        return _entMan.TryGetComponent<MessengerCartridgeComponent>(cartUid, out var comp)
               && TryGetPda(cartUid, out _, out var pda)
               && IsStationCrew(comp.Address, pda);
    }
}
