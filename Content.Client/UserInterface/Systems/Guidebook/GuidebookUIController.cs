using System.Linq;
using Content.Client.Gameplay;
using Content.Client.Guidebook;
using Content.Client.Guidebook.Controls;
using Content.Client.Lobby;
using Content.Client.Players.PlayTimeTracking;
//HONK START - popout window
using Content.Client.RussStation.Guidebook;
//HONK END
using Content.Client.UserInterface.Controls;
using Content.Shared.CCVar;
using Content.Shared.Guidebook;
using Content.Shared.Input;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Configuration;
using static Robust.Client.UserInterface.Controls.BaseButton;
using Robust.Shared.Input.Binding;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.UserInterface.Systems.Guidebook;

public sealed class GuidebookUIController : UIController, IOnStateEntered<LobbyState>, IOnStateEntered<GameplayState>, IOnStateExited<LobbyState>, IOnStateExited<GameplayState>, IOnSystemChanged<GuidebookSystem>
{
    [UISystemDependency] private readonly GuidebookSystem _guidebookSystem = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IConfigurationManager _configuration = default!;
    [Dependency] private readonly JobRequirementsManager _jobRequirements = default!;

    private const int PlaytimeOpenGuidebook = 60;

    private GuidebookWindow? _guideWindow;
    private MenuButton? GuidebookButton => UIManager.GetActiveUIWidgetOrNull<MenuBar.Widgets.GameTopMenuBar>()?.GuidebookButton;
    private ProtoId<GuideEntryPrototype>? _lastEntry;
    // HONK START - popout (issue #580)
    private GuidebookPopoutWindow? _popoutWindow;
    private bool _suppressPopoutRestore;
    private bool IsPopoutOpen => _popoutWindow?.IsOpen ?? false;
    // HONK END

    public void OnStateEntered(LobbyState state)
    {
        HandleStateEntered(state);
    }

    public void OnStateEntered(GameplayState state)
    {
        HandleStateEntered(state);
    }

    private void HandleStateEntered(State state)
    {
        DebugTools.Assert(_guideWindow == null);

        // setup window
        _guideWindow = UIManager.CreateWindow<GuidebookWindow>();
        _guideWindow.OnClose += OnWindowClosed;
        _guideWindow.OnOpen += OnWindowOpen;
        // HONK START - popout wiring (issue #580)
        _guideWindow.PopoutButton.OnPressed += _ => OpenPopout();
        // HONK END

        if (state is LobbyState &&
            _jobRequirements.FetchOverallPlaytime() < TimeSpan.FromMinutes(PlaytimeOpenGuidebook))
        {
            OpenGuidebook();
            _guideWindow.RecenterWindow(new(0.5f, 0.5f));
            _guideWindow.SetPositionFirst();
        }

        // setup keybinding
        CommandBinds.Builder
            .Bind(ContentKeyFunctions.OpenGuidebook,
                InputCmdHandler.FromDelegate(_ => ToggleGuidebook()))
            .Register<GuidebookUIController>();
    }

    public void OnStateExited(LobbyState state)
    {
        HandleStateExited();
    }

    public void OnStateExited(GameplayState state)
    {
        HandleStateExited();
    }

    private void HandleStateExited()
    {
        if (_guideWindow == null)
            return;

        _guideWindow.OnClose -= OnWindowClosed;
        _guideWindow.OnOpen -= OnWindowOpen;

        // HONK START - teardown popout before disposing docked window (issue #580)
        ClosePopoutNoRestore();
        _popoutWindow?.Dispose();
        _popoutWindow = null;
        // HONK END

        // shutdown
        _guideWindow.Dispose();
        _guideWindow = null;
        CommandBinds.Unregister<GuidebookUIController>();
    }

    public void OnSystemLoaded(GuidebookSystem system)
    {
        _guidebookSystem.OnGuidebookOpen += OpenGuidebook;
    }

    public void OnSystemUnloaded(GuidebookSystem system)
    {
        _guidebookSystem.OnGuidebookOpen -= OpenGuidebook;
    }

    internal void UnloadButton()
    {
        if (GuidebookButton == null)
            return;

        GuidebookButton.OnPressed -= GuidebookButtonOnPressed;
    }

    internal void LoadButton()
    {
        if (GuidebookButton == null)
            return;

        GuidebookButton.OnPressed += GuidebookButtonOnPressed;
    }

    private void GuidebookButtonOnPressed(ButtonEventArgs obj)
    {
        ToggleGuidebook();
    }

    public void ToggleGuidebook()
    {
        if (_guideWindow == null)
            return;

        // HONK START - popout takes precedence over docked toggle (issue #580)
        if (IsPopoutOpen)
        {
            UIManager.ClickSound();
            ClosePopoutNoRestore();
            return;
        }
        // HONK END

        if (_guideWindow.IsOpen)
        {
            UIManager.ClickSound();
            _guideWindow.Close();
        }
        else
        {
            OpenGuidebook();
        }
    }

    private void OnWindowClosed()
    {
        if (GuidebookButton != null)
            GuidebookButton.Pressed = false;

        if (_guideWindow != null)
        {
            _guideWindow.ReturnContainer.Visible = false;
            _lastEntry = _guideWindow.LastEntry;
        }
    }

    private void OnWindowOpen()
    {
        if (GuidebookButton != null)
            GuidebookButton.Pressed = true;
    }

    /// <summary>
    ///     Opens or closes the guidebook.
    /// </summary>
    /// <param name="guides">What guides should be shown. If not specified, this will instead list all the entries</param>
    /// <param name="rootEntries">A list of guides that should form the base of the table of contents. If not specified,
    /// this will automatically simply be a list of all guides that have no parent.</param>
    /// <param name="forceRoot">This forces a singular guide to contain all other guides. This guide will
    /// contain its own children, in addition to what would normally be the root guides if this were not
    /// specified.</param>
    /// <param name="includeChildren">Whether or not to automatically include child entries. If false, this will ONLY
    /// show the specified entries</param>
    /// <param name="selected">The guide whose contents should be displayed when the guidebook is opened</param>
    public void OpenGuidebook(
        Dictionary<ProtoId<GuideEntryPrototype>, GuideEntry>? guides = null,
        List<ProtoId<GuideEntryPrototype>>? rootEntries = null,
        ProtoId<GuideEntryPrototype>? forceRoot = null,
        bool includeChildren = true,
        ProtoId<GuideEntryPrototype>? selected = null)
    {
        if (_guideWindow == null)
            return;

        if (GuidebookButton != null)
            GuidebookButton.SetClickPressed(!_guideWindow.IsOpen);

        if (guides == null)
        {
            guides = _prototypeManager.EnumeratePrototypes<GuideEntryPrototype>()
                .ToDictionary(x => new ProtoId<GuideEntryPrototype>(x.ID), x => (GuideEntry) x);
        }
        else if (includeChildren)
        {
            var oldGuides = guides;
            guides = new(oldGuides);
            foreach (var guide in oldGuides.Values)
            {
                RecursivelyAddChildren(guide, guides);
            }
        }

        if (selected == null)
        {
            if (_lastEntry is { } lastEntry && guides.ContainsKey(lastEntry))
            {
                selected = _lastEntry;
            }
            else
            {
                selected = _configuration.GetCVar(CCVars.DefaultGuide);
            }
        }
        _guideWindow.UpdateGuides(guides, rootEntries, forceRoot, selected);

        // Expand up to depth-2.
        _guideWindow.Tree.SetAllExpanded(false);
        _guideWindow.Tree.SetAllExpanded(true, 1);

        _guideWindow.OpenCenteredRight();
    }

    public void OpenGuidebook(
        List<ProtoId<GuideEntryPrototype>> guideList,
        List<ProtoId<GuideEntryPrototype>>? rootEntries = null,
        ProtoId<GuideEntryPrototype>? forceRoot = null,
        bool includeChildren = true,
        ProtoId<GuideEntryPrototype>? selected = null)
    {
        Dictionary<ProtoId<GuideEntryPrototype>, GuideEntry> guides = new();
        foreach (var guideId in guideList)
        {
            if (!_prototypeManager.TryIndex(guideId, out var guide))
            {
                Log.Error($"Encountered unknown guide prototype: {guideId}");
                continue;
            }
            guides.Add(guideId, guide);
        }

        OpenGuidebook(guides, rootEntries, forceRoot, includeChildren, selected);
    }

    public void CloseGuidebook()
    {
        if (_guideWindow == null)
            return;

        // HONK START - popout close path (issue #580)
        if (IsPopoutOpen)
        {
            UIManager.ClickSound();
            ClosePopoutNoRestore();
            return;
        }
        // HONK END

        if (_guideWindow.IsOpen)
        {
            UIManager.ClickSound();
            _guideWindow.Close();
        }
    }

    // HONK START - popout helpers (issue #580)
    private void OpenPopout()
    {
        if (_guideWindow == null || IsPopoutOpen)
            return;

        UIManager.ClickSound();

        if (_guideWindow.IsOpen)
        {
            _lastEntry = _guideWindow.LastEntry;
            _guideWindow.Close();
        }

        _popoutWindow ??= CreatePopoutWindow();
        _popoutWindow.HostContent(_guideWindow.PopoutContent);
        _popoutWindow.Show();
    }

    private GuidebookPopoutWindow CreatePopoutWindow()
    {
        var window = new GuidebookPopoutWindow();
        window.Closed += OnPopoutClosed;
        return window;
    }

    private void OnPopoutClosed()
    {
        // Closed fires for both OS-X-button and programmatic Close().
        // Always return the content to the docked window so state survives.
        if (_popoutWindow != null && _guideWindow != null)
        {
            var content = _popoutWindow.ReleaseContent();
            if (content != null)
                _guideWindow.ContentsContainer.AddChild(content);
        }

        // Only re-open docked when the user closed via OS chrome. Programmatic
        // close paths (F1 toggle, state exit) set _suppressPopoutRestore first.
        if (!_suppressPopoutRestore && _guideWindow != null)
            OpenGuidebook(selected: _lastEntry);
    }

    private void ClosePopoutNoRestore()
    {
        if (_popoutWindow is not { IsOpen: true })
            return;

        _suppressPopoutRestore = true;
        try
        {
            _popoutWindow.Close();
        }
        finally
        {
            _suppressPopoutRestore = false;
        }
    }
    // HONK END

    private void RecursivelyAddChildren(GuideEntry guide, Dictionary<ProtoId<GuideEntryPrototype>, GuideEntry> guides)
    {
        foreach (var childId in guide.Children)
        {
            if (guides.ContainsKey(childId))
                continue;

            if (!_prototypeManager.TryIndex(childId, out var child))
            {
                Log.Error($"Encountered unknown guide prototype: {childId} as a child of {guide.Id}. If the child is not a prototype, it must be directly provided.");
                continue;
            }

            guides.Add(childId, child);
            RecursivelyAddChildren(child, guides);
        }
    }
}
