using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Maths;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client.RussStation.UI;

/// <summary>
///     Fork-standard multi-select filter tray. Use this in place of Robust's
///     <c>MultiselectOptionButton&lt;TKey&gt;</c> whenever a window wants a
///     filter control next to a search bar — it keeps the filter checkboxes
///     visible alongside the results instead of hiding them behind a dropdown,
///     and all fork windows should share this look.
/// </summary>
/// <remarks>
///     Same API surface as the upstream dropdown (<see cref="AddItem"/>,
///     <see cref="SelectedKeys"/>, <see cref="SelectedLabels"/>,
///     <see cref="DeselectAll"/>, <see cref="OnItemSelected"/>) so existing
///     callers drop in with a type swap. <see cref="Header"/> and
///     <see cref="Body"/> are separate Controls: mount the header inline
///     with the search row and the body in a vertical slot below. Right-click
///     the header to reset selection and fire per-key callbacks.
/// </remarks>
public sealed class HonkFilterPanel<TKey> where TKey : notnull
{
    // Thickness args are (left, top, right, bottom). The outer panel sits with a tighter
    // gap to the header than to surrounding siblings, hence top and the other three differ.
    private const int BorderPx = 1;
    private const int BodyMarginSidePx = 4;         // left + right + bottom
    private const int BodyMarginTopPx = 2;          // tighter gap to the header above
    private const int BodyPaddingSidePx = 10;       // inner content left + right
    private const int BodyPaddingTopBottomPx = 6;   // inner content top + bottom

    // Dark translucent slate so the body reads as a sub-panel against the window background.
    private static readonly Color BodyBackgroundColor = new(0.12f, 0.12f, 0.16f, 0.85f);
    // Medium grey border, just enough to separate the panel from siblings.
    private static readonly Color BodyBorderColor = new(0.35f, 0.35f, 0.45f, 1f);

    private readonly Dictionary<TKey, CheckBox> _checkboxes = new();
    private readonly Dictionary<TKey, string> _labels = new();
    private readonly HashSet<TKey> _selected = new();
    private string _labelBase = string.Empty;

    public Button Header { get; }
    public PanelContainer Body { get; }
    private readonly BoxContainer _bodyColumn;

    public IReadOnlyCollection<TKey> SelectedKeys => _selected;

    public IEnumerable<string> SelectedLabels
    {
        get
        {
            foreach (var key in _selected)
            {
                if (_labels.TryGetValue(key, out var label))
                    yield return label;
            }
        }
    }

    public string Label
    {
        get => _labelBase;
        set
        {
            _labelBase = value;
            UpdateHeaderText();
        }
    }

    public event Action<ItemPressedEventArgs>? OnItemSelected;

    public HonkFilterPanel()
    {
        _bodyColumn = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
        };
        Body = new PanelContainer
        {
            Margin = new Thickness(BodyMarginSidePx, BodyMarginTopPx, BodyMarginSidePx, BodyMarginSidePx),
            Visible = false,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = BodyBackgroundColor,
                BorderColor = BodyBorderColor,
                BorderThickness = new Thickness(BorderPx),
                ContentMarginLeftOverride = BodyPaddingSidePx,
                ContentMarginRightOverride = BodyPaddingSidePx,
                ContentMarginTopOverride = BodyPaddingTopBottomPx,
                ContentMarginBottomOverride = BodyPaddingTopBottomPx,
            },
            Children = { _bodyColumn },
        };
        Header = new Button { ToggleMode = true };
        Header.OnToggled += args => Body.Visible = args.Pressed;
        Header.OnKeyBindDown += args =>
        {
            if (args.Function != EngineKeyFunctions.UIRightClick)
                return;
            ResetAndNotify();
            args.Handle();
        };

        UpdateHeaderText();
    }

    public void AddItem(string label, TKey key)
    {
        _labels[key] = label;
        var cb = new CheckBox { Text = label };
        cb.OnToggled += args =>
        {
            if (args.Pressed)
                _selected.Add(key);
            else
                _selected.Remove(key);
            UpdateHeaderText();
            OnItemSelected?.Invoke(new ItemPressedEventArgs(key, args.Pressed));
        };
        _checkboxes[key] = cb;
        _bodyColumn.AddChild(cb);
    }

    public void DeselectAll()
    {
        if (_selected.Count == 0)
            return;
        _selected.Clear();
        foreach (var cb in _checkboxes.Values)
            cb.Pressed = false;
        UpdateHeaderText();
    }

    // Like DeselectAll but also fires OnItemSelected for every key that was cleared,
    // so subscribers (search-result rebuilders, label updaters) can react.
    private void ResetAndNotify()
    {
        if (_selected.Count == 0)
            return;
        var previouslySelected = new List<TKey>(_selected);
        DeselectAll();
        foreach (var key in previouslySelected)
            OnItemSelected?.Invoke(new ItemPressedEventArgs(key, false));
    }

    private void UpdateHeaderText()
    {
        Header.Text = _selected.Count == 0
            ? _labelBase
            : $"{_labelBase} ({_selected.Count})";
    }

    public sealed record ItemPressedEventArgs(TKey Key, bool Selected);
}
