using Robust.Client.UserInterface.Controls;

namespace Content.Client.RussStation.UI;

/// <summary>
///     Fork-standard search + filter-type bar for records consoles (station, criminal,
///     medical, etc.). Packages a type OptionButton next to a live-filtering LineEdit
///     so every records window has the same ergonomics: pick a field, start typing,
///     results update each keystroke. Right-click the LineEdit to clear (handled
///     globally by <see cref="RightClickClearTextBoxController"/>).
/// </summary>
/// <remarks>
///     Callers place this in XAML in the search row, populate the type options
///     via <see cref="AddTypeOption"/>, and subscribe <see cref="OnFilterChanged"/>
///     for a single (typeId, text) callback covering both axes. The underlying
///     <see cref="TypeOption"/> and <see cref="SearchBox"/> are exposed for cases
///     where the caller needs to set placeholder text, width, or seed state.
/// </remarks>
public sealed class HonkRecordsSearchBar : BoxContainer
{
    public OptionButton TypeOption { get; }
    public LineEdit SearchBox { get; }

    public int SelectedTypeId => TypeOption.SelectedId;
    public string Text
    {
        get => SearchBox.Text;
        // Skip the write while the user is typing: stale state echoes from the
        // server would otherwise clobber keystrokes that landed after the
        // echoed filter was sent.
        set
        {
            if (SearchBox.HasKeyboardFocus())
                return;
            SearchBox.SetText(value, invokeEvent: false);
        }
    }

    public event Action<HonkRecordsFilterChangedArgs>? OnFilterChanged;

    public HonkRecordsSearchBar()
    {
        Orientation = LayoutOrientation.Horizontal;
        HorizontalExpand = true;

        TypeOption = new OptionButton
        {
            MinWidth = 200,
            Margin = new Thickness(0, 0, 10, 0),
        };
        SearchBox = new LineEdit
        {
            HorizontalExpand = true,
        };

        AddChild(TypeOption);
        AddChild(SearchBox);

        TypeOption.OnItemSelected += args =>
        {
            TypeOption.SelectId(args.Id);
            Raise();
        };
        SearchBox.OnTextChanged += _ => Raise();
    }

    public void AddTypeOption(int id, string label) => TypeOption.AddItem(label, id);

    public void SelectTypeId(int id)
    {
        if (TypeOption.SelectedId == id)
            return;
        TypeOption.SelectId(id);
    }

    public void SetPlaceholder(string placeholder) => SearchBox.PlaceHolder = placeholder;

    private void Raise() => OnFilterChanged?.Invoke(new HonkRecordsFilterChangedArgs(TypeOption.SelectedId, SearchBox.Text));
}

public readonly record struct HonkRecordsFilterChangedArgs(int TypeId, string Text);
