using Content.Shared.Body;
using Content.Shared.Examine;
using Content.Shared.RussStation.Skillchips;
using Content.Shared.RussStation.Skillchips.Consumers;
using Content.Shared.RussStation.Skillchips.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.RussStation.Skillchips;

/// <summary>
/// Verifies the examine-hook skillchip consumers: <see cref="SharedIdAppraiserSystem"/> appends
/// an origin line (CentCom / Syndicate / station) when a capable examiner inspects an appraisable
/// ID, and <see cref="SharedDiskVerifierSystem"/> appends a forgery warning when a capable examiner
/// inspects a forged nuke disk. Both are gated on capability and details range.
/// </summary>
[TestFixture]
[TestOf(typeof(SharedIdAppraiserSystem))]
[TestOf(typeof(SharedDiskVerifierSystem))]
public sealed class SkillchipExamineConsumerTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: skillchip
  id: ExamineConsumerTestChip
  name: Examine Consumer Test Chip
  capacityCost: 1
  grants:
  - !type:CapabilityTagGrant
    tag: id_appraisal
  - !type:CapabilityTagGrant
    tag: disk_verifier

- type: entity
  id: ExamineConsumerTestBrain
  components:
  - type: Brain
  - type: Organ
    category: Brain
  - type: SkillchipHolder

- type: entity
  id: ExamineConsumerTestBody
  components:
  - type: Body
";

    private static EntityUid MakeCapableExaminer(
        IEntityManager em,
        SharedSkillchipSystem skillchips,
        SharedContainerSystem containers,
        EntityCoordinates coords)
    {
        var brain = em.SpawnEntity("ExamineConsumerTestBrain", coords);
        var body = em.SpawnEntity("ExamineConsumerTestBody", coords);
        var holder = em.GetComponent<SkillchipHolderComponent>(brain);

        skillchips.TryInstall((brain, holder), "ExamineConsumerTestChip");
        var container = containers.EnsureContainer<Container>(body, BodyComponent.ContainerID);
        containers.Insert(brain, container);

        return body;
    }

    private static string Examine(IEntityManager em, EntityUid examined, EntityUid examiner, bool details = true)
    {
        var ev = new ExaminedEvent(new FormattedMessage(), examined, examiner, details, hasDescription: false);
        em.EventBus.RaiseLocalEvent(examined, ev);
        return ev.GetTotalMessage().ToString();
    }

    [Test]
    public async Task IdAppraiserReportsOriginTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var em = server.ResolveDependency<IEntityManager>();
        var loc = server.ResolveDependency<ILocalizationManager>();
        var skillchips = em.System<SharedSkillchipSystem>();
        var containers = em.System<SharedContainerSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var examiner = MakeCapableExaminer(em, skillchips, containers, mapData.GridCoords);

            var stationId = em.SpawnEntity(null, mapData.GridCoords);
            em.AddComponent<IdAppraisableComponent>(stationId);
            Assert.That(Examine(em, stationId, examiner),
                Does.Contain(loc.GetString("skillchip-id-appraisal-examine-station")),
                "A plain appraisable ID should read as station-issued.");

            var centcomId = em.SpawnEntity(null, mapData.GridCoords);
            em.AddComponent<IdAppraisableComponent>(centcomId);
            em.AddComponent<CentcomIssuedComponent>(centcomId);
            Assert.That(Examine(em, centcomId, examiner),
                Does.Contain(loc.GetString("skillchip-id-appraisal-examine-centcom")),
                "A CentCom-flagged ID should read as CentCom-issued.");

            var syndieId = em.SpawnEntity(null, mapData.GridCoords);
            em.AddComponent<IdAppraisableComponent>(syndieId);
            em.AddComponent<SyndicateIssuedComponent>(syndieId);
            Assert.That(Examine(em, syndieId, examiner),
                Does.Contain(loc.GetString("skillchip-id-appraisal-examine-syndicate")),
                "A Syndicate-flagged ID should read as off-console.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task IdAppraiserSilentWithoutCapabilityTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var em = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            // A plain examiner with no skillchip capability.
            var examiner = em.SpawnEntity(null, mapData.GridCoords);
            var id = em.SpawnEntity(null, mapData.GridCoords);
            em.AddComponent<IdAppraisableComponent>(id);

            Assert.That(Examine(em, id, examiner), Is.Empty,
                "Without the id_appraisal capability nothing should be appended.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DiskVerifierFlagsForgeryOnlyTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var em = server.ResolveDependency<IEntityManager>();
        var skillchips = em.System<SharedSkillchipSystem>();
        var containers = em.System<SharedContainerSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var examiner = MakeCapableExaminer(em, skillchips, containers, mapData.GridCoords);

            var forgedDisk = em.SpawnEntity(null, mapData.GridCoords);
            var forgedComp = em.AddComponent<NukeDiskVerifiableComponent>(forgedDisk);
            forgedComp.IsForgery = true;
            Assert.That(Examine(em, forgedDisk, examiner), Is.Not.Empty,
                "A capable examiner should get a warning on a forged disk.");

            var genuineDisk = em.SpawnEntity(null, mapData.GridCoords);
            em.AddComponent<NukeDiskVerifiableComponent>(genuineDisk); // IsForgery stays false
            Assert.That(Examine(em, genuineDisk, examiner), Is.Empty,
                "A genuine disk should produce no warning (silence = all good).");

            // Out of details range: no warning even on a forgery.
            Assert.That(Examine(em, forgedDisk, examiner, details: false), Is.Empty,
                "The warning should only appear within details range.");
        });

        await pair.CleanReturnAsync();
    }
}
