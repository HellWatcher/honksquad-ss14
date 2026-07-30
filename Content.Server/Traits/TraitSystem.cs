using Content.Shared.GameTicking;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Roles;
using Content.Shared.Traits;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;
// HONK START - #634: merge ReplacementAccent list instead of skip-if-exists.
using Content.Server.Speech.Components;
// HONK END

namespace Content.Server.Traits;

public sealed partial class TraitSystem : EntitySystem
{
    [Dependency] private SharedHandsSystem _sharedHandsSystem = default!;
    [Dependency] private EntityWhitelistSystem _whitelistSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
    }

    // When the player is spawned in, add all trait components selected during character creation
    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        // Check if player's job allows to apply traits
        if (args.JobId == null ||
            !ProtoMan.Resolve<JobPrototype>(args.JobId, out var protoJob) ||
            !protoJob.ApplyTraits)
        {
            return;
        }

        foreach (var traitId in args.Profile.TraitPreferences)
        {
            if (!ProtoMan.TryIndex<TraitPrototype>(traitId, out var traitPrototype))
            {
                Log.Error($"No trait found with ID {traitId}!");
                return;
            }

            if (_whitelistSystem.IsWhitelistFail(traitPrototype.Whitelist, args.Mob) ||
                _whitelistSystem.IsWhitelistPass(traitPrototype.Blacklist, args.Mob))
                continue;

            // Add all components required by the prototype
            if (traitPrototype.Components.Count > 0)
            {
                // HONK START - #634: ReplacementAccent is a list component now. AddComponents would skip it
                // because the entity may already have one from the species baseline (e.g. Dwarf scottish).
                // Instead merge the trait's accent ids into the existing list so they compose.
                MergeTraitReplacementAccents(args.Mob, traitPrototype.Components);
                // HONK END
                EntityManager.AddComponents(args.Mob, traitPrototype.Components, false);
            }

            // Add all JobSpecials required by the prototype
            foreach (var special in traitPrototype.Specials)
            {
                special.AfterEquip(args.Mob);
            }

            // Add item required by the trait
            if (traitPrototype.TraitGear == null)
                continue;

            if (!TryComp(args.Mob, out HandsComponent? handsComponent))
                continue;

            var coords = Transform(args.Mob).Coordinates;
            var inhandEntity = Spawn(traitPrototype.TraitGear, coords);
            _sharedHandsSystem.TryPickup(args.Mob,
                inhandEntity,
                checkActionBlocker: false,
                handsComp: handsComponent);
        }
    }

    // HONK START - #634: merge a trait's ReplacementAccent entries into the existing list on the mob so accents
    // compose (Dwarf + Liar = scottish + liar, not one clobbering the other). Runs before AddComponents - when
    // the mob already has a ReplacementAccent, AddComponents will skip re-adding the trait's copy thanks to its
    // skip-if-exists behaviour with removeExisting=false, and the merged list sticks.
    private void MergeTraitReplacementAccents(EntityUid mob, ComponentRegistry components)
    {
        if (!TryComp<ReplacementAccentComponent>(mob, out var existing))
            return;

        var name = Factory.GetComponentName(typeof(ReplacementAccentComponent));
        if (!components.TryGetValue(name, out var entry))
            return;

        if (entry.Component is not ReplacementAccentComponent traitComp)
            return;

        // Legacy singular form needs folding too - the trait's ComponentInit would have done it after AddComp,
        // but we're merging into an existing component here so handle both shapes.
        if (traitComp.Accent is { } legacy && !existing.Accents.Contains(legacy))
            existing.Accents.Add(legacy);

        foreach (var accentId in traitComp.Accents)
        {
            if (!existing.Accents.Contains(accentId))
                existing.Accents.Add(accentId);
        }
    }
    // HONK END
}
