using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Graphics;
using Robust.Shared.Input;
using Robust.Shared.Utility;
//HONK START - fork short-form label helper, autofit helper, font kind, UIFontSize CVars
using Content.Client.RussStation.UI;
using Content.Client.Stylesheets.Fonts;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
//HONK END

namespace Content.Client.UserInterface.Controls;

public sealed class MenuButton : ContainerButton
{
    [Dependency] private readonly IInputManager _inputManager = default!;
    public const string StyleClassLabelTopButton = "topButtonLabel";
    // public const string StyleClassRedTopButton = "topButtonLabel";

    // TODO: KIIIIIILLLLLLLLLLLLLLLLLLLLLLLLLLL --kaylie.
    private static readonly Color ColorNormal = Color.FromHex("#99a7b3"); // primary color[0] + 0.24 L
    private static readonly Color ColorHovered = Color.FromHex("#acbac6"); // primary color[0] + 0.30 L
    private static readonly Color ColorPressed = Color.FromHex("#75838e"); // primary color[0] + 0.12 L

    private const float VertPad = 4f;

    private BoundKeyFunction? _function;
    private readonly BoxContainer _root;
    private readonly TextureRect? _buttonIcon;
    private readonly Label? _buttonLabel;

    public string AppendStyleClass { set => AddStyleClass(value); }
    public Texture? Icon { get => _buttonIcon!.Texture; set => _buttonIcon!.Texture = value; }

    public BoundKeyFunction? BoundKey
    {
        get => _function;
        set
        {
            _function = value;
            //HONK START - replaces the inline `_buttonLabel!.Text = ...` with fork short-form + autofit
            HonkUpdateKeyLabel();
            //HONK END
        }
    }

    //HONK START - text uses the fork short-form helper (modifier bindings visible) AND
    // autofits so it tracks the user's UIFontSize accessibility setting. The button itself
    // is free to grow to fit whatever font size results. At the game's default UI font
    // (12pt) topButtonLabel renders at 14pt bold; the fitter scales proportionally with
    // the user's CVar. The width/height budget is intentionally generous — we want the
    // button to grow, not for the text to shrink.
    private const int HonkKeyLabelBaseSize = 14;
    private const float HonkKeyLabelMaxWidthPx = 200f;
    private const float HonkKeyLabelMaxHeightPx = 100f;

    private void HonkUpdateKeyLabel()
    {
        if (_buttonLabel == null)
            return;
        var keyString = _function == null ? "" : HonkKeyLabel.For(_function.Value);
        _buttonLabel.Text = keyString;
        if (string.IsNullOrEmpty(keyString))
            return;

        HonkLabelFitter.Fit(
            _buttonLabel,
            keyString,
            HonkKeyLabelMaxWidthPx,
            baseSizeAtGameDefault: HonkKeyLabelBaseSize,
            kind: FontKind.Bold,
            targetHeightPx: HonkKeyLabelMaxHeightPx);
    }
    //HONK END

    public BoxContainer ButtonRoot => _root;

    public MenuButton()
    {
        IoCManager.InjectDependencies(this);
        _buttonIcon = new TextureRect()
        {
            TextureScale = new Vector2(0.5f, 0.5f),
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            VerticalExpand = true,
            Margin = new Thickness(0, VertPad),
            ModulateSelfOverride = ColorNormal,
            Stretch = TextureRect.StretchMode.KeepCentered
        };
        _buttonLabel = new Label
        {
            Text = "",
            HorizontalAlignment = HAlignment.Center,
            ModulateSelfOverride = ColorNormal,
            StyleClasses = {StyleClassLabelTopButton}
        };
        _root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Children =
            {
                _buttonIcon,
                _buttonLabel
            }
        };
        AddChild(_root);
        ToggleMode = true;
    }

    protected override void EnteredTree()
    {
        _inputManager.OnKeyBindingAdded += OnKeyBindingChanged;
        _inputManager.OnKeyBindingRemoved += OnKeyBindingChanged;
        _inputManager.OnInputModeChanged += OnKeyBindingChanged;
        //HONK START - refit when the user changes UI font size or family
        var cfg = IoCManager.Resolve<IConfigurationManager>();
        cfg.OnValueChanged(CCVars.UIFontSize, HonkOnFontSizeChanged);
        cfg.OnValueChanged(CCVars.UIFontFamily, HonkOnFontFamilyChanged);
        //HONK END
    }

    protected override void ExitedTree()
    {
        _inputManager.OnKeyBindingAdded -= OnKeyBindingChanged;
        _inputManager.OnKeyBindingRemoved -= OnKeyBindingChanged;
        _inputManager.OnInputModeChanged -= OnKeyBindingChanged;
        //HONK START - unsubscribe UI font CVar handlers
        var cfg = IoCManager.Resolve<IConfigurationManager>();
        cfg.UnsubValueChanged(CCVars.UIFontSize, HonkOnFontSizeChanged);
        cfg.UnsubValueChanged(CCVars.UIFontFamily, HonkOnFontFamilyChanged);
        //HONK END
    }

    //HONK START - CVar change handlers for live UIFontSize / UIFontFamily refit.
    private void HonkOnFontSizeChanged(int _) => HonkUpdateKeyLabel();
    private void HonkOnFontFamilyChanged(string _) => HonkUpdateKeyLabel();
    //HONK END


    private void OnKeyBindingChanged(IKeyBinding obj)
    {
        //HONK START - fork short-form label with modifier support + autofit
        HonkUpdateKeyLabel();
        //HONK END
    }

    private void OnKeyBindingChanged()
    {
        //HONK START
        HonkUpdateKeyLabel();
        //HONK END
    }

    protected override void StylePropertiesChanged()
    {
        // colors of children depend on style, so ensure we update when style is changed
        base.StylePropertiesChanged();
        UpdateChildColors();
    }

    private void UpdateChildColors()
    {
        if (_buttonIcon == null || _buttonLabel == null) return;
        switch (DrawMode)
        {
            case DrawModeEnum.Normal:
                _buttonIcon.ModulateSelfOverride = ColorNormal;
                _buttonLabel.ModulateSelfOverride = ColorNormal;
                break;

            case DrawModeEnum.Pressed:
                _buttonIcon.ModulateSelfOverride = ColorPressed;
                _buttonLabel.ModulateSelfOverride = ColorPressed;
                break;

            case DrawModeEnum.Hover:
                _buttonIcon.ModulateSelfOverride = ColorHovered;
                _buttonLabel.ModulateSelfOverride = ColorHovered;
                break;

            case DrawModeEnum.Disabled:
                break;
        }
    }


    protected override void DrawModeChanged()
    {
        base.DrawModeChanged();
        UpdateChildColors();
    }
}
