using Content.Client.Gameplay;
using Content.Client.Viewport;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.ResourceManagement;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Configuration;

namespace Content.Client.RussStation.HoverTooltip;

public sealed class EntityHoverTooltipSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _configManager = default!;
    [Dependency] private readonly IInputManager _inputManager = default!;
    [Dependency] private readonly IOverlayManager _overlayManager = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;
    [Dependency] private readonly IStateManager _stateManager = default!;
    [Dependency] private readonly IUserInterfaceManager _uiManager = default!;

    private EntityHoverTooltipOverlay _overlay = default!;
    private EntityUid? _hoveredEntity;
    private float _hoverTime;
    private bool _enabled;
    private float _delay;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new EntityHoverTooltipOverlay(_resourceCache);
        _overlayManager.AddOverlay(_overlay);

        Subs.CVar(_configManager, CCVars.HoverTooltipEnabled, v => _enabled = v, true);
        Subs.CVar(_configManager, CCVars.HoverTooltipDelay, v => _delay = v, true);

        UpdatesAfter.Add(typeof(SharedEyeSystem));
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlayManager.RemoveOverlay(_overlay);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (!_enabled)
        {
            _overlay.Visible = false;
            return;
        }

        if (_stateManager.CurrentState is not GameplayStateBase screen)
        {
            _overlay.Visible = false;
            return;
        }

        EntityUid? entityToClick = null;

        if (_uiManager.CurrentlyHovered is IViewportControl vp
            && _inputManager.MouseScreenPosition.IsValid)
        {
            var mousePosWorld = vp.PixelToMap(_inputManager.MouseScreenPosition.Position);

            if (vp is ScalingViewport svp)
                entityToClick = screen.GetClickedEntity(mousePosWorld, svp.Eye);
            else
                entityToClick = screen.GetClickedEntity(mousePosWorld);
        }

        if (entityToClick != _hoveredEntity)
        {
            _hoverTime = 0f;
            _hoveredEntity = entityToClick;
        }

        if (_hoveredEntity == null || Deleted(_hoveredEntity))
        {
            _hoveredEntity = null;
            _overlay.Visible = false;
            return;
        }

        _hoverTime += frameTime;

        if (_hoverTime < _delay)
        {
            _overlay.Visible = false;
            return;
        }

        var name = MetaData(_hoveredEntity.Value).EntityName;
        if (string.IsNullOrEmpty(name))
        {
            _overlay.Visible = false;
            return;
        }

        _overlay.TooltipText = name;
        _overlay.ScreenPosition = _inputManager.MouseScreenPosition.Position;
        _overlay.Visible = true;
    }
}
