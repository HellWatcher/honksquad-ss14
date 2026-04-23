using System.Linq;
using System.Numerics;
using Content.Client.Actions;
using Content.Client.Construction;
using Content.Client.Gameplay;
using Content.Client.Hands;
using Content.Client.Interaction;
using Content.Client.Outline;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.Actions.Controls;
using Content.Client.UserInterface.Systems.Actions.Widgets;
using Content.Client.UserInterface.Systems.Actions.Windows;
using Content.Client.UserInterface.Systems.Gameplay;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Charges.Systems;
using Content.Shared.Input;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using static Content.Client.Actions.ActionsSystem;
using static Content.Client.UserInterface.Systems.Actions.Windows.ActionsWindow;
using static Robust.Client.UserInterface.Control;
using static Robust.Client.UserInterface.Controls.BaseButton;
using static Robust.Client.UserInterface.Controls.LineEdit;
//HONK - filter control replaced with the inline HonkFilterPanel; nested event args come from there
using static Content.Client.RussStation.UI.HonkFilterPanel<
    Content.Client.UserInterface.Systems.Actions.Windows.ActionsWindow.Filters>;
using static Robust.Client.UserInterface.Controls.TextureRect;
using static Robust.Shared.Input.Binding.PointerInputCmdHandler;

namespace Content.Client.UserInterface.Systems.Actions;

public sealed class ActionUIController : UIController, IOnStateChanged<GameplayState>, IOnSystemChanged<ActionsSystem>
{
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IInputManager _input = default!;

    [UISystemDependency] private readonly ActionsSystem? _actionsSystem = default;
    [UISystemDependency] private readonly InteractionOutlineSystem? _interactionOutline = default;
    [UISystemDependency] private readonly TargetOutlineSystem? _targetOutline = default;
    [UISystemDependency] private readonly SpriteSystem _spriteSystem = default!;

    private ActionButtonContainer? _container;
    private readonly List<EntityUid?> _actions = new();
    private readonly DragDropHelper<ActionButton> _menuDragHelper;
    private readonly TextureRect _dragShadow;
    private ActionsWindow? _window;

    private ActionsBar? ActionsBar => UIManager.GetActiveUIWidgetOrNull<ActionsBar>();
    private MenuButton? ActionButton => UIManager.GetActiveUIWidgetOrNull<MenuBar.Widgets.GameTopMenuBar>()?.ActionButton;

    public bool IsDragging => _menuDragHelper.IsDragging;

    /// <summary>
    /// Action slot we are currently selecting a target for.
    /// </summary>
    public EntityUid? SelectingTargetFor { get; private set; }

    public ActionUIController()
    {
        _menuDragHelper = new DragDropHelper<ActionButton>(OnMenuBeginDrag, OnMenuContinueDrag, OnMenuEndDrag);
        _dragShadow = new TextureRect
        {
            MinSize = new Vector2(64, 64),
            Stretch = StretchMode.Scale,
            Visible = false,
            SetSize = new Vector2(64, 64),
            MouseFilter = MouseFilterMode.Ignore
        };
    }

    public override void Initialize()
    {
        base.Initialize();

        var gameplayStateLoad = UIManager.GetUIController<GameplayStateLoadController>();
        gameplayStateLoad.OnScreenLoad += OnScreenLoad;
        gameplayStateLoad.OnScreenUnload += OnScreenUnload;
    }

    private void OnScreenLoad()
    {
       LoadGui();
    }

    private void OnScreenUnload()
    {
        UnloadGui();
    }

    public void OnStateEntered(GameplayState state)
    {
        if (_actionsSystem != null)
        {
            _actionsSystem.OnActionAdded += OnActionAdded;
            _actionsSystem.OnActionRemoved += OnActionRemoved;
            _actionsSystem.ActionsUpdated += OnActionsUpdated;

            //HONK START - catch emote actions whose OnActionAdded fired before the subscription
            // above (initial ActionsComponent state is applied while we're still in LobbyState).
            // Non-emote actions come back through LoadDefaultActions on link; emote actions have
            // autoPopulate: false and would otherwise miss their saved slot restoration.
            var custom = UIManager.GetUIController<Content.Client.RussStation.ActionBar.ActionBarCustomizationController>();
            foreach (var existing in _actionsSystem.GetClientActions())
            {
                if (!EntityManager.TryGetComponent<Content.Shared.RussStation.VerbBindings.HonkEmoteActionComponent>(existing.Owner, out var emoteTag))
                    continue;
                if (_actions.Contains(existing))
                    continue;
                if (!custom.TryGetSavedEmoteSlot(emoteTag.Emote, out var savedSlot))
                    continue;
                if (savedSlot < 0 || savedSlot >= ContentKeyFunctions.GetHotbarBoundKeys().Length)
                    continue;
                while (_actions.Count <= savedSlot)
                    _actions.Add(null);
                if (_actions[savedSlot] == null)
                    _actions[savedSlot] = existing.Owner;
            }
            // Push the updated _actions into the bar; without this the placement is in our
            // list but never reaches the visible container.
            if (_container != null)
                _container.SetActionData(_actionsSystem, _actions.ToArray());
            //HONK END
        }

        UpdateFilterLabel();
        QueueWindowUpdate();

        _dragShadow.Orphan();
        UIManager.PopupRoot.AddChild(_dragShadow);

        var builder = CommandBinds.Builder;
        var hotbarKeys = ContentKeyFunctions.GetHotbarBoundKeys();
        for (var i = 0; i < hotbarKeys.Length; i++)
        {
            var boundId = i; // This is needed, because the lambda captures it.
            var boundKey = hotbarKeys[i];
            builder = builder.Bind(boundKey, new PointerInputCmdHandler((in PointerInputCmdArgs args) =>
            {
                if (args.State != BoundKeyState.Down)
                    return false;

                TriggerAction(boundId);
                return true;
            }, false, true));
        }

        builder
            .Bind(ContentKeyFunctions.OpenActionsMenu,
                InputCmdHandler.FromDelegate(_ => ToggleWindow()))
            .BindBefore(EngineKeyFunctions.Use, new PointerInputCmdHandler(TargetingOnUse, outsidePrediction: true),
                    typeof(ConstructionSystem), typeof(DragDropSystem))
                .BindBefore(EngineKeyFunctions.UIRightClick, new PointerInputCmdHandler(TargetingCancel, outsidePrediction: true))
            .Register<ActionUIController>();
    }

    private bool TargetingCancel(in PointerInputCmdArgs args)
    {
        if (!_timing.IsFirstTimePredicted)
            return false;

        // only do something for actual target-based actions
        if (SelectingTargetFor == null)
            return false;

        StopTargeting();
        return true;
    }

    /// <summary>
    ///     If the user clicked somewhere, and they are currently targeting an action, try and perform it.
    /// </summary>
    private bool TargetingOnUse(in PointerInputCmdArgs args)
    {
        if (!_timing.IsFirstTimePredicted || _actionsSystem == null || SelectingTargetFor is not { } actionId)
            return false;

        if (_playerManager.LocalEntity is not { } user)
            return false;

        if (!EntityManager.TryGetComponent<ActionsComponent>(user, out var comp))
            return false;

        if (_actionsSystem.GetAction(actionId) is not {} action ||
            !EntityManager.TryGetComponent<TargetActionComponent>(action, out var target))
        {
            return false;
        }

        // Is the action currently valid?
        if (!_actionsSystem.ValidAction(action))
        {
            // The user is targeting with this action, but it is not valid. Maybe mark this click as
            // handled and prevent further interactions.
            return !target.InteractOnMiss;
        }

        var ev = new ActionTargetAttemptEvent(args, (user, comp), action);
        EntityManager.EventBus.RaiseLocalEvent(action, ref ev);
        if (!ev.Handled)
        {
            Log.Error($"Action {EntityManager.ToPrettyString(actionId)} did not handle ActionTargetAttemptEvent!");
            return false;
        }

        // stop targeting when needed
        if (ev.FoundTarget ? !target.Repeat : target.DeselectOnMiss)
            StopTargeting();

        return true;
    }

    public void UnloadButton()
    {
        if (ActionButton != null)
            ActionButton.OnPressed -= ActionButtonPressed;
    }

    public void LoadButton()
    {
        if (ActionButton != null)
            ActionButton.OnPressed += ActionButtonPressed;
    }

    private void OnWindowOpened()
    {
        ActionButton?.SetClickPressed(true);

        SearchAndDisplay();
    }

    private void OnWindowClosed()
    {
        ActionButton?.SetClickPressed(false);
    }

    public void OnStateExited(GameplayState state)
    {
        if (_actionsSystem != null)
        {
            _actionsSystem.OnActionAdded -= OnActionAdded;
            _actionsSystem.OnActionRemoved -= OnActionRemoved;
            _actionsSystem.ActionsUpdated -= OnActionsUpdated;
        }

        CommandBinds.Unregister<ActionUIController>();
    }

    private void TriggerAction(int index)
    {
        if (!_actions.TryGetValue(index, out var actionId) ||
            _actionsSystem?.GetAction(actionId) is not {} action)
        {
            return;
        }

        // TODO: probably should have a clientside event raised for flexibility
        if (EntityManager.TryGetComponent<TargetActionComponent>(action, out var target))
            ToggleTargeting((action, action, target));
        else
            _actionsSystem?.TriggerAction(action);
    }

    //HONK START - remember which slot an action from a given provider item last occupied, so
    // hand<->pocket moves (which revoke + regrant the action) don't shuffle the bar layout.
    private readonly Dictionary<EntityUid, int> _honkLastSlotByProvider = new();
    //HONK END

    private void OnActionAdded(EntityUid actionId)
    {
        if (_actionsSystem?.GetAction(actionId) is not {} action)
            return;

        // TODO: event
        // if the action is toggled when we add it, start targeting
        if (action.Comp.Toggled && EntityManager.TryGetComponent<TargetActionComponent>(actionId, out var target))
            StartTargeting((action, action, target));

        if (_actions.Contains(action))
            return;

        //HONK START - fork add-to-bar gating.
        // * Emote actions never auto-add regardless of the global toggle; they stay in the
        //   actions menu until the player drags one onto a slot.
        // * Hand/pocket swaps of an item that already had a slot always restore to that slot
        //   (player accepted the action onto the bar previously, so a move shouldn't silently
        //   drop it). The trailing-null trim in OnActionRemoved means the remembered slot can
        //   be past _actions.Count by the time we re-add, so pad up to it (capped at the bound
        //   hotbar key count).
        // * Truly new providers fall through to the auto-add CVar.
        if (EntityManager.TryGetComponent<Content.Shared.RussStation.VerbBindings.HonkEmoteActionComponent>(actionId, out var emoteTag))
        {
            // Emotes normally stay in the menu, but a player's saved placement from a prior
            // session wins: if the emote's proto id has a remembered slot we're free to fill,
            // drop it there. Go through the controller directly so its Initialize (and CVar
            // parse) is guaranteed to have run before we read the map.
            var custom = UIManager.GetUIController<Content.Client.RussStation.ActionBar.ActionBarCustomizationController>();
            if (custom.TryGetSavedEmoteSlot(emoteTag.Emote, out var savedSlot)
                && savedSlot >= 0
                && savedSlot < ContentKeyFunctions.GetHotbarBoundKeys().Length)
            {
                while (_actions.Count <= savedSlot)
                    _actions.Add(null);
                if (_actions[savedSlot] == null)
                {
                    _actions[savedSlot] = action;
                    return;
                }
            }
            return;
        }
        if (action.Comp.Container is {} provider
            && _honkLastSlotByProvider.TryGetValue(provider, out var lastSlot)
            && lastSlot >= 0
            && lastSlot < ContentKeyFunctions.GetHotbarBoundKeys().Length)
        {
            while (_actions.Count <= lastSlot)
                _actions.Add(null);
            if (_actions[lastSlot] == null)
            {
                _actions[lastSlot] = action;
                return;
            }
        }
        if (!Content.Client.RussStation.ActionBar.ActionBarCustomizationController.AutoAddActions)
            return;
        //HONK END

        _actions.Add(action);
    }

    private void OnActionRemoved(EntityUid actionId)
    {
        if (_container == null)
            return;

        if (actionId == SelectingTargetFor)
            StopTargeting();

        //HONK START - null the slot in place + record provider->slot so re-granted actions return home
        for (var i = 0; i < _actions.Count; i++)
        {
            if (_actions[i] != actionId)
                continue;
            if (_actionsSystem?.GetAction(actionId) is {} action && action.Comp.Container is {} provider)
                _honkLastSlotByProvider[provider] = i;
            _actions[i] = null;
        }
        while (_actions.Count > 0 && _actions[^1] == null)
            _actions.RemoveAt(_actions.Count - 1);
        //HONK END
    }

    private void OnActionsUpdated()
    {
        QueueWindowUpdate();

        if (_actionsSystem != null)
            _container?.SetActionData(_actionsSystem, _actions.ToArray());
    }

    //HONK START - public entry so the fork controller can force a hotbar rebuild when empty-slot /
    // slots-per-row / rows CVars change and the padding needs to grow or shrink; and a slot-trigger
    // entry point so SlotHotkeyController can dispatch after resolving a key→slot mapping that
    // differs from the upstream fixed index.
    public void HonkRefreshHotbar() => OnActionsUpdated();

    public void HonkTriggerSlot(int slot) => TriggerAction(slot);
    //HONK END

    private void ActionButtonPressed(ButtonEventArgs args)
    {
        ToggleWindow();
    }

    private void ToggleWindow()
    {
        if (_window == null)
            return;

        if (_window.IsOpen)
        {
            _window.Close();
            return;
        }

        _window.Open();
    }

    private void UpdateFilterLabel()
    {
        if (_window == null)
            return;

        if (_window.FilterButton.SelectedKeys.Count == 0)
        {
            _window.FilterLabel.Visible = false;
        }
        else
        {
            _window.FilterLabel.Visible = true;
            _window.FilterLabel.Text = Loc.GetString("ui-actionmenu-filter-label",
                ("selectedLabels", string.Join(", ", _window.FilterButton.SelectedLabels)));
        }
    }

    private bool MatchesFilter(Entity<ActionComponent> ent, Filters filter)
    {
        var (uid, comp) = ent;
        return filter switch
        {
            Filters.Enabled => comp.Enabled,
            Filters.Item => comp.Container != null && comp.Container != _playerManager.LocalEntity,
            Filters.Innate => comp.Container == null || comp.Container == _playerManager.LocalEntity,
            Filters.Instant => EntityManager.HasComponent<InstantActionComponent>(uid),
            Filters.Targeted => EntityManager.HasComponent<TargetActionComponent>(uid),
            //HONK START - emote-as-action filter (#579)
            Filters.Emote => EntityManager.HasComponent<Content.Shared.RussStation.VerbBindings.HonkEmoteActionComponent>(uid),
            //HONK END
            _ => throw new ArgumentOutOfRangeException(nameof(filter), filter, null)
        };
    }

    private void ClearList()
    {
        if (_window?.Disposed == false)
            _window.ResultsGrid.RemoveAllChildren();
    }

    private void PopulateActions(IEnumerable<Entity<ActionComponent>> actions)
    {
        if (_window is not { Disposed: false, IsOpen: true })
            return;

        if (_actionsSystem == null)
            return;

        _window.UpdateNeeded = false;

        List<ActionButton> existing = new(_window.ResultsGrid.ChildCount);
        foreach (var child in _window.ResultsGrid.Children)
        {
            if (child is ActionButton button)
                existing.Add(button);
        }

        int i = 0;
        foreach (var action in actions)
        {
            if (i < existing.Count)
            {
                existing[i++].UpdateData(action, _actionsSystem);
                continue;
            }

            var button = new ActionButton(EntityManager, _spriteSystem, this) {Locked = true};
            button.ActionPressed += OnWindowActionPressed;
            button.ActionUnpressed += OnWindowActionUnPressed;
            button.ActionFocusExited += OnWindowActionFocusExisted;
            button.UpdateData(action, _actionsSystem);
            _window.ResultsGrid.AddChild(button);
        }

        for (; i < existing.Count; i++)
        {
            existing[i].Dispose();
        }
    }

    public void QueueWindowUpdate()
    {
        if (_window != null)
            _window.UpdateNeeded = true;
    }

    private void SearchAndDisplay()
    {
        if (_window is not { Disposed: false, IsOpen: true })
            return;

        if (_actionsSystem == null)
            return;

        if (_playerManager.LocalEntity is not { } player)
            return;

        var search = _window.SearchBar.Text;
        var filters = _window.FilterButton.SelectedKeys;
        var actions = _actionsSystem.GetClientActions();

        if (filters.Count == 0 && string.IsNullOrWhiteSpace(search))
        {
            PopulateActions(actions);
            return;
        }

        actions = actions.Where(action =>
        {
            if (filters.Count > 0 && filters.Any(filter => !MatchesFilter(action, filter)))
                return false;

            if (action.Comp.Keywords.Any(keyword => search.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                return true;

            var name = EntityManager.GetComponent<MetaDataComponent>(action).EntityName;
            if (name.Contains(search, StringComparison.OrdinalIgnoreCase))
                return true;

            if (action.Comp.Container == null || action.Comp.Container == player)
                return false;

            var providerName = EntityManager.GetComponent<MetaDataComponent>(action.Comp.Container.Value).EntityName;
            return providerName.Contains(search, StringComparison.OrdinalIgnoreCase);
        });

        PopulateActions(actions);
    }

    private void SetAction(ActionButton button, EntityUid? actionId, bool updateSlots = true)
    {
        if (_actionsSystem == null)
            return;

        int position;

        if (actionId == null)
        {
            button.ClearData();
            if (_container?.TryGetButtonIndex(button, out position) ?? false)
            {
                //HONK START - keep _actions sparse so a cleared middle slot doesn't shift later actions
                // left, and forget any emote-slot memory for whatever was in this slot so it doesn't
                // come back on reconnect.
                if (position >= 0 && position < _actions.Count)
                {
                    if (_actions[position] is { } clearedAction
                        && EntityManager.TryGetComponent<Content.Shared.RussStation.VerbBindings.HonkEmoteActionComponent>(clearedAction, out var clearedEmote))
                    {
                        // Only forget the saved slot if the emote hasn't already been moved to a
                        // different slot in this same drag (DragAction does place-then-clear, so the
                        // new slot is already in the map by the time we reach the clear).
                        var custom = UIManager.GetUIController<Content.Client.RussStation.ActionBar.ActionBarCustomizationController>();
                        if (custom.TryGetSavedEmoteSlot(clearedEmote.Emote, out var savedPos) && savedPos == position)
                            custom.HonkRememberEmoteSlot(clearedEmote.Emote, null);
                    }
                    _actions[position] = null;
                    while (_actions.Count > 0 && _actions[^1] == null)
                        _actions.RemoveAt(_actions.Count - 1);
                }
                //HONK END
            }
        }
        else if (button.TryReplaceWith(actionId.Value, _actionsSystem) &&
            _container != null &&
            _container.TryGetButtonIndex(button, out position))
        {
            //HONK START - pad with nulls so an action dropped on slot N lands at slot N, not at Count,
            // update provider->slot memory so re-acquiring the item later restores to the new slot,
            // and persist emote placements so disconnect+reconnect restores the same layout.
            while (_actions.Count < position)
                _actions.Add(null);
            if (_actionsSystem.GetAction(actionId.Value) is {} placedAction
                && placedAction.Comp.Container is {} placedProvider)
            {
                _honkLastSlotByProvider[placedProvider] = position;
            }
            if (EntityManager.TryGetComponent<Content.Shared.RussStation.VerbBindings.HonkEmoteActionComponent>(actionId.Value, out var placedEmote))
            {
                UIManager.GetUIController<Content.Client.RussStation.ActionBar.ActionBarCustomizationController>()
                    .HonkRememberEmoteSlot(placedEmote.Emote, position);
            }
            //HONK END
            if (position >= _actions.Count)
            {
                _actions.Add(actionId);
            }
            else
            {
                _actions[position] = actionId;
            }
        }

        if (updateSlots)
            _container?.SetActionData(_actionsSystem, _actions.ToArray());
    }

    private void DragAction()
    {
        if (_menuDragHelper.Dragged is not {Action: {} action} dragged)
        {
            _menuDragHelper.EndDrag();
            return;
        }

        EntityUid? swapAction = null;
        var currentlyHovered = UIManager.MouseGetControl(_input.MouseScreenPosition);
        if (currentlyHovered is ActionButton button)
        {
            swapAction = button.Action;
            SetAction(button, action, false);
        }

        if (dragged.Parent is ActionButtonContainer)
            SetAction(dragged, swapAction, false);

        if (_actionsSystem != null)
            _container?.SetActionData(_actionsSystem, _actions.ToArray());

        _menuDragHelper.EndDrag();
    }

    //HONK - OnClearPressed removed; right-click the search box clears search text via the global handler

    private void OnSearchChanged(LineEditEventArgs args)
    {
        QueueWindowUpdate();
    }

    private void OnFilterSelected(ItemPressedEventArgs args)
    {
        UpdateFilterLabel();
        QueueWindowUpdate();
    }

    private void OnWindowActionPressed(GUIBoundKeyEventArgs args, ActionButton action)
    {
        if (args.Function != EngineKeyFunctions.UIClick && args.Function != EngineKeyFunctions.Use)
            return;

        HandleActionPressed(args, action);
    }

    private void OnWindowActionUnPressed(GUIBoundKeyEventArgs args, ActionButton dragged)
    {
        if (args.Function != EngineKeyFunctions.UIClick && args.Function != EngineKeyFunctions.Use)
            return;

        HandleActionUnpressed(args, dragged);
    }

    private void OnWindowActionFocusExisted(ActionButton button)
    {
        _menuDragHelper.EndDrag();
    }

    private void OnActionPressed(GUIBoundKeyEventArgs args, ActionButton button)
    {
        if (args.Function == EngineKeyFunctions.UIRightClick)
        {
            //HONK START - locked bars swallow right-click clear so a mis-click can't wipe a slot
            if (Content.Client.RussStation.ActionBar.ActionBarCustomizationController.LockActions)
            {
                args.Handle();
                return;
            }
            //HONK END
            SetAction(button, null);
            args.Handle();
            return;
        }

        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        HandleActionPressed(args, button);
    }

    private void HandleActionPressed(GUIBoundKeyEventArgs args, ActionButton button)
    {
        args.Handle();

        //HONK START - assign-hotkey mode: left-click arms the clicked slot so the
        // next hotbar keypress becomes that slot's hotkey. Short-circuit the drag
        // and trigger paths entirely while assign mode is on.
        var slotHotkeys = UIManager.GetUIController<Content.Client.RussStation.ActionBar.SlotHotkeyController>();
        if (slotHotkeys.AssignMode
            && _container != null
            && _container.TryGetButtonIndex(button, out var armSlot))
        {
            slotHotkeys.ArmSlot(armSlot);
            return;
        }
        //HONK END

        if (button.Action != null)
        {
            //HONK START - lock blocks drag-rearrange on the bar; clicking an action to fire it still works
            if (Content.Client.RussStation.ActionBar.ActionBarCustomizationController.LockActions)
                return;
            //HONK END
            _menuDragHelper.MouseDown(button);
            return;
        }

        // good job
    }

    private void OnActionUnpressed(GUIBoundKeyEventArgs args, ActionButton button)
    {
        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        HandleActionUnpressed(args, button);
    }

    private void HandleActionUnpressed(GUIBoundKeyEventArgs args, ActionButton button)
    {
        if (_actionsSystem == null)
            return;

        args.Handle();

        //HONK START - assign-hotkey mode: swallow release so the action doesn't trigger on a
        // slot the player was arming for rebind. Arming already happened in HandleActionPressed.
        if (UIManager.GetUIController<Content.Client.RussStation.ActionBar.SlotHotkeyController>().AssignMode)
            return;
        //HONK END

        if (_menuDragHelper.IsDragging)
        {
            DragAction();
            return;
        }

        _menuDragHelper.EndDrag();

        if (button.Action is not {} action)
            return;

        // TODO: make this an event
        if (!EntityManager.TryGetComponent<TargetActionComponent>(action, out var target))
        {
            _actionsSystem?.TriggerAction(action);
            return;
        }

        // for target actions, we go into "select target" mode, we don't
        // message the server until we actually pick our target.

        // if we're clicking the same thing we're already targeting for, then we simply cancel
        // targeting
        ToggleTargeting((action, action.Comp, target));
    }

    private bool OnMenuBeginDrag()
    {
        //HONK START - pad empty drop targets into the bar for the duration of the drag
        UIManager.GetUIController<Content.Client.RussStation.ActionBar.ActionBarCustomizationController>().HonkSetDragActive(true);
        //HONK END
        // TODO ACTIONS
        // The dragging icon shuld be based on the entity's icon style. I.e. if the action has a large icon texture,
        // and a small item/provider sprite, then the dragged icon should be the big texture, not the provider.
        if (_menuDragHelper.Dragged?.Action is {} action)
        {
            if (EntityManager.TryGetComponent(action.Comp.EntityIcon, out SpriteComponent? sprite)
                && sprite.Icon?.GetFrame(RsiDirection.South, 0) is {} frame)
            {
                _dragShadow.Texture = frame;
            }
            else if (action.Comp.Icon is {} icon)
            {
                _dragShadow.Texture = _spriteSystem.Frame0(icon);
            }
            else
            {
                _dragShadow.Texture = null;
            }
        }

        LayoutContainer.SetPosition(_dragShadow, UIManager.MousePositionScaled.Position - new Vector2(32, 32));
        return true;
    }

    private bool OnMenuContinueDrag(float frameTime)
    {
        LayoutContainer.SetPosition(_dragShadow, UIManager.MousePositionScaled.Position - new Vector2(32, 32));
        _dragShadow.Visible = true;
        return true;
    }

    private void OnMenuEndDrag()
    {
        //HONK START - trim the drag-only empty slots back out now that the drop is resolved
        UIManager.GetUIController<Content.Client.RussStation.ActionBar.ActionBarCustomizationController>().HonkSetDragActive(false);
        //HONK END
        _dragShadow.Texture = null;
        _dragShadow.Visible = false;
    }

    private void UnloadGui()
    {
        _actionsSystem?.UnlinkAllActions();

        if (ActionsBar == null)
        {
            return;
        }

        if (_window != null)
        {
            _window.OnOpen -= OnWindowOpened;
            _window.OnClose -= OnWindowClosed;
            //HONK - ClearButton removed; right-click the search box to clear it
            _window.SearchBar.OnTextChanged -= OnSearchChanged;
            _window.FilterButton.OnItemSelected -= OnFilterSelected;

            _window.Dispose();
            _window = null;
        }
    }

    private void LoadGui()
    {
        UnloadGui();
        _window = UIManager.CreateWindow<ActionsWindow>();
        LayoutContainer.SetAnchorPreset(_window, LayoutContainer.LayoutPreset.CenterTop);

        _window.OnOpen += OnWindowOpened;
        _window.OnClose += OnWindowClosed;
        //HONK START - ClearButton removed (right-click the search box instead); wire fork
        // lock + auto-add checkboxes on the actions window.
        UIManager.GetUIController<Content.Client.RussStation.ActionBar.ActionBarCustomizationController>().HonkBindWindow(_window);
        //HONK END
        _window.SearchBar.OnTextChanged += OnSearchChanged;
        _window.FilterButton.OnItemSelected += OnFilterSelected;

        if (ActionsBar == null)
        {
            return;
        }

        RegisterActionContainer(ActionsBar.ActionsContainer);

        //HONK START - apply fork layout (rows, slots-per-row, spacing, empty preview, min-slot padding)
        // once the container exists and before LinkAllActions populates it; a second call after
        // linking re-asserts the layout in case upstream rebuilds the grid during linking.
        UIManager.GetUIController<Content.Client.RussStation.ActionBar.ActionBarCustomizationController>().HonkOnContainerReady();
        //HONK END

        _actionsSystem?.LinkAllActions();

        //HONK START - re-apply after the initial action link rebuilds the container children
        UIManager.GetUIController<Content.Client.RussStation.ActionBar.ActionBarCustomizationController>().HonkOnContainerReady();
        //HONK END
    }

    public void RegisterActionContainer(ActionButtonContainer container)
    {
        if (_container != null)
        {
            _container.ActionPressed -= OnActionPressed;
            _container.ActionUnpressed -= OnActionUnpressed;
        }

        _container = container;
        _container.ActionPressed += OnActionPressed;
        _container.ActionUnpressed += OnActionUnpressed;
    }

    private void ClearActions()
    {
        _container?.ClearActionData();
    }

    private void AssignSlots(List<SlotAssignment> assignments)
    {
        if (_actionsSystem == null)
            return;

        _actions.Clear();
        foreach (var assign in assignments)
        {
            _actions.Add(assign.ActionId);
        }

        _container?.SetActionData(_actionsSystem, _actions.ToArray());
    }

    public void RemoveActionContainer()
    {
        _container = null;
    }

    public void OnSystemLoaded(ActionsSystem system)
    {
        system.LinkActions += OnComponentLinked;
        system.UnlinkActions += OnComponentUnlinked;
        system.ClearAssignments += ClearActions;
        system.AssignSlot += AssignSlots;
    }

    public void OnSystemUnloaded(ActionsSystem system)
    {
        system.LinkActions -= OnComponentLinked;
        system.UnlinkActions -= OnComponentUnlinked;
        system.ClearAssignments -= ClearActions;
        system.AssignSlot -= AssignSlots;
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        _menuDragHelper.Update(args.DeltaSeconds);
        if (_window is {UpdateNeeded: true})
            SearchAndDisplay();
    }

    private void OnComponentLinked(ActionsComponent component)
    {
        if (_actionsSystem == null)
            return;

        LoadDefaultActions();
        _container?.SetActionData(_actionsSystem, _actions.ToArray());
        QueueWindowUpdate();
    }

    private void OnComponentUnlinked()
    {
        _container?.ClearActionData();
        QueueWindowUpdate();
        StopTargeting();
        //HONK START - reset the first-link flag so next connection gets the default bar populated
        // even if auto-add is off; within a connection the guard preserves the curated layout on respawn.
        _honkLoadedDefaultsThisConnection = false;
        //HONK END
    }

    //HONK START - track whether defaults have been populated this connection so auto-add off still
    // gets a sensible initial bar on first server link. Reset on unlink-all-actions (disconnect).
    private bool _honkLoadedDefaultsThisConnection;
    //HONK END

    private void LoadDefaultActions()
    {
        if (_actionsSystem == null)
            return;

        //HONK START - auto-add off skips the respawn / body-swap repopulate so a curated bar survives,
        // but first link per connection always loads defaults so a new connect isn't a blank bar.
        if (_honkLoadedDefaultsThisConnection
            && !Content.Client.RussStation.ActionBar.ActionBarCustomizationController.AutoAddActions)
            return;
        _honkLoadedDefaultsThisConnection = true;
        //HONK END

        var actions = _actionsSystem.GetClientActions().Where(action => action.Comp.AutoPopulate).ToList();
        actions.Sort(ActionComparer);

        _actions.Clear();
        foreach (var (action, _) in actions)
        {
            if (!_actions.Contains(action))
                _actions.Add(action);
        }
    }

    /// <summary>
    /// If currently targeting with this slot, stops targeting.
    /// If currently targeting with no slot or a different slot, switches to
    /// targeting with the specified slot.
    /// </summary>
    private void ToggleTargeting(Entity<ActionComponent, TargetActionComponent> ent)
    {
        if (SelectingTargetFor == ent)
        {
            StopTargeting();
            return;
        }

        StartTargeting(ent);
    }

    /// <summary>
    /// Puts us in targeting mode, where we need to pick either a target point or entity
    /// </summary>
    private void StartTargeting(Entity<ActionComponent, TargetActionComponent> ent)
    {
        var (uid, action, target) = ent;

        // If we were targeting something else we should stop
        StopTargeting();

        SelectingTargetFor = uid;
        // TODO inform the server
        _actionsSystem?.SetToggled(uid, true);

        // override "held-item" overlay
        var provider = action.Container;

        if (target.TargetingIndicator && _overlays.TryGetOverlay<ShowHandItemOverlay>(out var handOverlay))
        {
            if (action.ItemIconStyle == ItemActionIconStyle.BigItem && action.Container != null)
            {
                handOverlay.EntityOverride = provider;
            }
            else if (action.Toggled && action.IconOn != null)
                handOverlay.IconOverride = _spriteSystem.Frame0(action.IconOn);
            else if (action.Icon != null)
                handOverlay.IconOverride = _spriteSystem.Frame0(action.Icon);
        }

        if (_container != null)
        {
            foreach (var button in _container.GetButtons())
            {
                if (button.Action?.Owner == uid)
                    button.UpdateIcons();
            }
        }

        // TODO: allow world-targets to check valid positions. E.g., maybe:
        // - Draw a red/green ghost entity
        // - Add a yes/no checkmark where the HandItemOverlay usually is

        // Highlight valid entity targets
        if (!EntityManager.TryGetComponent<EntityTargetActionComponent>(uid, out var entity))
            return;

        Func<EntityUid, bool>? predicate = null;
        var attachedEnt = action.AttachedEntity;

        if (!entity.CanTargetSelf)
            predicate = e => e != attachedEnt;

        var range = target.CheckCanAccess ? target.Range : -1;

        _interactionOutline?.SetEnabled(false);
        _targetOutline?.Enable(range, target.CheckCanAccess, predicate, entity.Whitelist, entity.Blacklist, null);
    }

    /// <summary>
    /// Switch out of targeting mode if currently selecting target for an action
    /// </summary>
    private void StopTargeting()
    {
        if (SelectingTargetFor == null)
            return;

        var oldAction = SelectingTargetFor;
        // TODO inform the server
        _actionsSystem?.SetToggled(oldAction, false);

        SelectingTargetFor = null;

        _targetOutline?.Disable();
        _interactionOutline?.SetEnabled(true);

        if (_container != null)
        {
            foreach (var button in _container.GetButtons())
            {
                if (button.Action?.Owner == oldAction)
                    button.UpdateIcons();
            }
        }

        if (!_overlays.TryGetOverlay<ShowHandItemOverlay>(out var handOverlay))
            return;

        handOverlay.IconOverride = null;
        handOverlay.EntityOverride = null;
    }
}
