// HONK: fork-side OS-window host for the guidebook (issue #580).
// The docked GuidebookWindow's SplitContainer is reparented into this window
// while popout mode is active, then returned when the window closes.
using System;
using Content.Client.Guidebook.Controls;
using Content.Client.Guidebook.RichText;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.RussStation.Guidebook;

public sealed class GuidebookPopoutWindow : OSWindow, ILinkClickHandler, IAnchorClickHandler
{
    // The link/anchor click tags walk up the visual tree looking for a handler. Once the
    // guidebook content is reparented under this OSWindow the walk never reaches the docked
    // GuidebookWindow, so the popout has to satisfy both interfaces and forward to the
    // original window which holds the entry/prototype state.
    private GuidebookWindow? _source;

    public GuidebookPopoutWindow()
    {
        Title = Loc.GetString("honk-guidebook-popout-title");
        SetWidth = 900;
        SetHeight = 700;
        StartupLocation = WindowStartupLocation.CenterOwner;
    }

    public void HostContent(GuidebookWindow source, Control content)
    {
        _source = source;
        content.Orphan();
        AddChild(content);
    }

    public Control? ReleaseContent()
    {
        _source = null;
        if (ChildCount == 0)
            return null;

        var content = GetChild(0);
        content.Orphan();
        return content;
    }

    public void HandleClick(string link) => _source?.HandleClick(link);

    public void HandleAnchor(IPrototypeLinkControl prototypeLinkControl) => _source?.HandleAnchor(prototypeLinkControl);
}
