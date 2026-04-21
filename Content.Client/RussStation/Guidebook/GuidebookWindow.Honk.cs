// HONK: exposes the private Split container so the popout window can reparent it (issue #580).
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Guidebook.Controls;

public sealed partial class GuidebookWindow
{
    public SplitContainer PopoutContent => Split;
}
