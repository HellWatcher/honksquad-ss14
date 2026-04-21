// HONK: fork-side OS-window host for the guidebook (issue #580).
// The docked GuidebookWindow's SplitContainer is reparented into this window
// while popout mode is active, then returned when the window closes.
using System;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.RussStation.Guidebook;

public sealed class GuidebookPopoutWindow : OSWindow
{
    public GuidebookPopoutWindow()
    {
        Title = Loc.GetString("honk-guidebook-popout-title");
        SetWidth = 900;
        SetHeight = 700;
        StartupLocation = WindowStartupLocation.CenterOwner;
    }

    public void HostContent(Control content)
    {
        content.Orphan();
        AddChild(content);
    }

    public Control? ReleaseContent()
    {
        if (ChildCount == 0)
            return null;

        var content = GetChild(0);
        content.Orphan();
        return content;
    }
}
