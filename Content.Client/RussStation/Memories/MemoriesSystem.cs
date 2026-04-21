using Content.Client.CharacterInfo;
using Content.Client.Message;
using Content.Shared.RussStation.Memories;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.RussStation.Memories;

public sealed class MemoriesSystem : EntitySystem
{
    [Dependency] private readonly CharacterInfoSystem _characterInfo = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MemoriesComponent, AfterAutoHandleStateEvent>(OnHandleState);
        SubscribeLocalEvent<CharacterInfoSystem.GetCharacterInfoControlsEvent>(OnGetCharacterInfoControls);
    }

    private void OnHandleState(EntityUid uid, MemoriesComponent component, ref AfterAutoHandleStateEvent args)
    {
        _characterInfo.RequestCharacterInfo();
    }

    private void OnGetCharacterInfoControls(ref CharacterInfoSystem.GetCharacterInfoControlsEvent ev)
    {
        if (!TryComp<MemoriesComponent>(ev.Entity, out var memories) || memories.Memories.Count == 0)
            return;

        var box = new BoxContainer
        {
            Margin = new Thickness(MemoriesConstants.PanelMargin),
            Orientation = BoxContainer.LayoutOrientation.Vertical
        };

        var title = new RichTextLabel
        {
            HorizontalAlignment = Control.HAlignment.Center
        };
        title.SetMarkup(Loc.GetString("memories-panel-header"));
        box.AddChild(title);

        foreach (var (key, value) in memories.Memories)
        {
            var entry = new RichTextLabel();
            var localizedKey = Loc.GetString(key);
            entry.SetMarkup(Loc.GetString("memories-panel-entry", ("key", localizedKey), ("value", value)));
            box.AddChild(entry);
        }

        ev.Controls.Add(box);
    }
}
