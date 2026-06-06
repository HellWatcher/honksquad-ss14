using System.Numerics;
using Content.Client.UserInterface.Systems.Actions.Widgets;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.RussStation.ActionBar;

// HONK Owns the free-positioning behaviour of the action bar: lifting it out of its XAML
// parent into the screen LayoutContainer (and back), the drag-handle wiring, and the clamped
// drag maths. Reads and persists the position through ActionBarCVarManager so the controller
// stays out of the reparenting weeds.
public sealed class ActionBarPositioningManager
{
    private readonly IUserInterfaceManager _ui;
    private readonly ActionBarCVarManager _cvars;

    // Stashed when we lift the bar out of its XAML parent for free-positioning, so a reset to
    // anchored mode can drop it back at the same sibling index.
    private Control? _anchoredParent;
    private int _anchoredIndex;
    private LayoutContainer? _floatParent;

    // Live position, separate from the CVar value because a drag updates it continuously and
    // only persists to the CVar on release.
    private float _positionX;
    private float _positionY;

    public ActionBarPositioningManager(IUserInterfaceManager ui, ActionBarCVarManager cvars)
    {
        _ui = ui;
        _cvars = cvars;
        _positionX = cvars.PositionX;
        _positionY = cvars.PositionY;
    }

    public float PositionX => _positionX;
    public float PositionY => _positionY;

    private ActionsBar? GetBar() => _ui.GetActiveUIWidgetOrNull<ActionsBar>();

    public void WireDragHandle()
    {
        if (GetBar() is not { } bar)
            return;
        // Defensive: re-wiring on every container ready call would stack handlers, so
        // unsubscribe first.
        bar.HonkDragHandle.DragMoved -= OnHandleDragMoved;
        bar.HonkDragHandle.DragEnded -= OnHandleDragEnded;
        bar.HonkDragHandle.DragMoved += OnHandleDragMoved;
        bar.HonkDragHandle.DragEnded += OnHandleDragEnded;
        RefreshDragHandleVisibility();
    }

    public void RefreshDragHandleVisibility()
    {
        if (GetBar() is { } bar)
            bar.HonkDragHandle.Visible = !ActionBarRuntimeConfig.Current.LockActions;
    }

    // Re-sync the live position from the (now changed) CVars and re-apply. Wired to the CVar
    // manager's PositionChanged so options-menu applies, preset loads, and the drag-end persist
    // all reach the bar.
    public void OnPositionChanged()
    {
        _positionX = _cvars.PositionX;
        _positionY = _cvars.PositionY;
        ApplyPosition();
    }

    private void OnHandleDragMoved(Vector2 delta)
    {
        if (GetBar() is not { } bar)
            return;
        // Capture the bar's pre-reparent screen position so the first drag doesn't snap
        // the bar to (0,0) before we've written any explicit coordinates.
        var initial = bar.GlobalPosition;
        EnsureFloating(bar);
        if (_floatParent == null)
            return;
        if (_positionX < 0 || _positionY < 0)
        {
            var local = initial - _floatParent.GlobalPosition;
            _positionX = local.X;
            _positionY = local.Y;
        }
        var size = bar.Size;
        var bounds = _floatParent.Size;
        _positionX = Math.Clamp(_positionX + delta.X, ActionBarConstants.PositionEdgeMargin, MathF.Max(ActionBarConstants.PositionEdgeMargin, bounds.X - size.X - ActionBarConstants.PositionEdgeMargin));
        _positionY = Math.Clamp(_positionY + delta.Y, ActionBarConstants.PositionEdgeMargin, MathF.Max(ActionBarConstants.PositionEdgeMargin, bounds.Y - size.Y - ActionBarConstants.PositionEdgeMargin));
        LayoutContainer.SetPosition(bar, new Vector2(_positionX, _positionY));
    }

    private void OnHandleDragEnded() => _cvars.WritePosition(_positionX, _positionY);

    /// <summary>Applies the saved position to the bar, lifting it into the screen's
    /// LayoutContainer when set or restoring its XAML parent when reset to -1.</summary>
    public void ApplyPosition()
    {
        if (GetBar() is not { } bar)
            return;
        if (_positionX < 0 || _positionY < 0)
        {
            EnsureAnchored(bar);
            return;
        }
        EnsureFloating(bar);
        if (_floatParent == null)
            return;
        LayoutContainer.SetPosition(bar, new Vector2(_positionX, _positionY));
    }

    private void EnsureFloating(ActionsBar bar)
    {
        if (bar.Parent is LayoutContainer current && current == _floatParent)
            return;
        var origin = bar.Parent;
        if (origin == null)
            return;
        // Walk up to find the screen-level LayoutContainer (ViewportContainer in both
        // game screens) so SetPosition's attached props will actually be honoured.
        Control? walker = origin;
        LayoutContainer? layout = null;
        while (walker != null)
        {
            if (walker is LayoutContainer lc)
            {
                layout = lc;
                break;
            }
            walker = walker.Parent;
        }
        if (layout == null)
            return;
        if (_anchoredParent == null)
        {
            _anchoredParent = origin;
            _anchoredIndex = bar.GetPositionInParent();
        }
        origin.RemoveChild(bar);
        layout.AddChild(bar);
        _floatParent = layout;
    }

    private void EnsureAnchored(ActionsBar bar)
    {
        if (_floatParent == null || bar.Parent != _floatParent || _anchoredParent == null)
            return;
        _floatParent.RemoveChild(bar);
        _anchoredParent.AddChild(bar);
        // Keep the original sibling order so the menu bar / vote menu stack as before.
        var clamped = Math.Clamp(_anchoredIndex, 0, _anchoredParent.ChildCount - 1);
        bar.SetPositionInParent(clamped);
    }
}
