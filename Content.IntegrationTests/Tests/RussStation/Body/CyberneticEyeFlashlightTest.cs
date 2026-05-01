using System.Linq;
using Content.Server.RussStation.Body;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.RussStation.Body;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.RussStation.Body;

[TestFixture]
[TestOf(typeof(CyberneticEyeFlashlightSystem))]
public sealed class CyberneticEyeFlashlightTest
{
    private const string ConeMaskPath = "/Textures/Effects/LightMasks/cone.png";

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: CyberFlashlightTestEyes
  components:
  - type: Organ
    category: Eyes
  - type: CyberneticEyes
  - type: PointLight
    enabled: false
    mask: /Textures/Effects/LightMasks/cone.png
    autoRot: true
    radius: 6
    netsync: false
  - type: UnpoweredFlashlight

- type: entity
  id: CyberFlashlightTestBody
  components:
  - type: Body

- type: entity
  id: CyberFlashlightTestBodyWithEyes
  components:
  - type: Body
  - type: EntityTableContainerFill
    containers:
      body_organs: !type:AllSelector
        children:
        - id: CyberFlashlightTestEyes
";

    [Test]
    public async Task EyesInsertedConfigureFlashlightOnBodyTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var containerSys = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<SharedContainerSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entMan.SpawnEntity("CyberFlashlightTestBodyWithEyes", mapData.GridCoords);

            var organContainer = containerSys.GetContainer(body, BodyComponent.ContainerID);
            Assert.That(organContainer.OccludesLight, Is.False,
                "Body's organ container must stop occluding light so the eye PointLight reaches the world");

            var eyes = organContainer.ContainedEntities
                .First(o => entMan.HasComponent<CyberneticEyesComponent>(o));

            Assert.That(entMan.TryGetComponent<UnpoweredFlashlightComponent>(eyes, out var flashlight), Is.True,
                "Cybernetic eye organ should carry UnpoweredFlashlightComponent");
            Assert.That(flashlight!.LightOn, Is.False,
                "Light should start off; player has to toggle it on");
            Assert.That(flashlight.ToggleActionEntity, Is.Not.Null,
                "Toggle action entity should be granted to the body on insert");

            var lightSys = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<SharedPointLightSystem>();
            Assert.That(lightSys.TryGetLight(eyes, out var light), Is.True,
                "Cybernetic eye organ should carry a PointLight");
            Assert.That(light!.MaskPath, Is.EqualTo(ConeMaskPath),
                "PointLight should use the cone mask, matching upstream Flashlight prototype");
            Assert.That(light.MaskAutoRotate, Is.True,
                "Cone should rotate with body facing");
            Assert.That(light.Enabled, Is.False,
                "Light should default to off until the action is toggled");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ToggleActionTurnsLightOnAndOffTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var containerSys = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<SharedContainerSystem>();
        var flashlightSys = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<UnpoweredFlashlightSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entMan.SpawnEntity("CyberFlashlightTestBodyWithEyes", mapData.GridCoords);
            var organContainer = containerSys.GetContainer(body, BodyComponent.ContainerID);
            var eyes = organContainer.ContainedEntities
                .First(o => entMan.HasComponent<CyberneticEyesComponent>(o));

            var lightSys = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<SharedPointLightSystem>();

            flashlightSys.SetLight((eyes, null), true, quiet: true);

            var flashlight = entMan.GetComponent<UnpoweredFlashlightComponent>(eyes);
            Assert.That(flashlight.LightOn, Is.True);
            Assert.That(lightSys.TryGetLight(eyes, out var light), Is.True);
            Assert.That(light!.Enabled, Is.True, "Toggling on should enable the PointLight");

            flashlightSys.SetLight((eyes, null), false, quiet: true);
            Assert.That(flashlight.LightOn, Is.False);
            Assert.That(light.Enabled, Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EyesRemovedTearsDownFlashlightTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var containerSys = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<SharedContainerSystem>();
        var flashlightSys = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<UnpoweredFlashlightSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entMan.SpawnEntity("CyberFlashlightTestBodyWithEyes", mapData.GridCoords);
            var organContainer = containerSys.GetContainer(body, BodyComponent.ContainerID);
            var eyes = organContainer.ContainedEntities
                .First(o => entMan.HasComponent<CyberneticEyesComponent>(o));

            var lightSys = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<SharedPointLightSystem>();

            // Turn the light on, then yank the eyes -- the light should end up off.
            flashlightSys.SetLight((eyes, null), true, quiet: true);
            Assert.That(lightSys.TryGetLight(eyes, out var lightBefore), Is.True);
            Assert.That(lightBefore!.Enabled, Is.True, "Precondition: light is on before removal");

            containerSys.Remove(eyes, organContainer);

            var flashlight = entMan.GetComponent<UnpoweredFlashlightComponent>(eyes);
            Assert.That(flashlight.LightOn, Is.False,
                "LightOn should be cleared so re-installing the eyes starts in a known-off state");
            Assert.That(lightSys.TryGetLight(eyes, out var lightAfter), Is.True);
            Assert.That(lightAfter!.Enabled, Is.False,
                "PointLight should be disabled when the eyes leave the body");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BodyWithoutEyesHasNoFlashlightTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entMan.SpawnEntity("CyberFlashlightTestBody", mapData.GridCoords);

            Assert.That(entMan.HasComponent<UnpoweredFlashlightComponent>(body), Is.False,
                "Body without cybernetic eyes should not carry the flashlight component");
        });

        await pair.CleanReturnAsync();
    }
}
