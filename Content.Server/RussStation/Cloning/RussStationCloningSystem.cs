using Content.Shared.Cloning;
using Content.Shared.Cloning.Events;
using Content.Shared.Humanoid;
using Robust.Shared.Prototypes;

namespace Content.Server.RussStation.Cloning;

/// <summary>
/// Copies fork-side trait components onto clones via a side-channel
/// CloningSettings prototype, so adding a new fork trait does not require
/// editing upstream clone.yml.
/// </summary>
public sealed class RussStationCloningSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;

    /// <summary>
    /// ID of the fork-side cloningSettings prototype whose Components hashset
    /// lists every trait component the fork wants copied during cloning.
    /// </summary>
    public static readonly ProtoId<CloningSettingsPrototype> ForkExtensionsId = "RussStationBodyExtensions";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HumanoidProfileComponent, CloningEvent>(OnCloned);
    }

    private void OnCloned(Entity<HumanoidProfileComponent> ent, ref CloningEvent args)
    {
        if (!_proto.TryIndex(ForkExtensionsId, out var extensions))
            return;

        foreach (var componentName in extensions.Components)
        {
            if (!Factory.TryGetRegistration(componentName, out var registration))
            {
                Log.Error($"RussStationCloningSystem: invalid component name in {ForkExtensionsId}: {componentName}");
                continue;
            }

            // Mirror upstream CloneComponents: clone gets the component iff the original has it.
            RemComp(args.CloneUid, registration.Type);
            if (EntityManager.TryGetComponent(ent.Owner, registration.Type, out var sourceComp))
                CopyComp(ent.Owner, args.CloneUid, sourceComp);
        }
    }
}
