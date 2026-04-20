using System.Linq;
//HONK START
using Content.Client.RussStation.Stylesheets;
//HONK END
using Content.Client.Stylesheets.Fonts;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;
using static Robust.Client.UserInterface.StylesheetHelpers;


namespace Content.Client.Stylesheets.Stylesheets;

[Virtual]
public partial class SystemStylesheet : CommonStylesheet
{
    public override string StylesheetName => "System";

    public override NotoFontFamilyStack BaseFont { get; } // TODO: NotoFontFamilyStack is temporary

    public override Dictionary<Type, ResPath[]> Roots => new()
    {
        { typeof(TextureResource), [] },
    };

    private const int PrimaryFontSize = 12;
    private const int FontSizeStep = 2;

    // for some GOD FORSAKEN REASON if I use a collection expression here it throws a sandbox error
    // Thanks ReSharper, this was very fun to find in the ~40 files I last committed
    // ReSharper disable once UseCollectionExpression
    //HONK START - non-readonly so ForkFontCustomization.Apply can reassign
    private List<(string?, int)> _commonFontSizes = new()
    //HONK END
    {
        (null, PrimaryFontSize),
        (StyleClass.FontSmall, PrimaryFontSize - FontSizeStep),
        (StyleClass.FontLarge, PrimaryFontSize + FontSizeStep),
    };

    public SystemStylesheet(object config, StylesheetManager man) : base(config)
    {
        BaseFont = new NotoFontFamilyStack(ResCache);

        //HONK START - Font customization (fork-owned helper)
        _commonFontSizes = ForkFontCustomization.Apply(BaseFont, man, PrimaryFontSize, FontSizeStep, _commonFontSizes);
        var customSize = man.FontManager.CurrentSize;
        //HONK END

        var rules = new[]
        {
            // Set up important rules that need to go first.
            GetRulesForFont(null, BaseFont, _commonFontSizes),
            // Set up our core rules.
            [
                // Declare the default font.
                //HONK START - use customSize so user-selected font size applies
                Element().Prop(Label.StylePropertyFont, BaseFont.GetFont(customSize)),
                //HONK END
            ],
            // Finally, load all the other sheetlets.
            GetAllSheetletRules<PalettedStylesheet, CommonSheetletAttribute>(man),
            GetAllSheetletRules<SystemStylesheet, CommonSheetletAttribute>(man),
        };

        Stylesheet = new Stylesheet(rules.SelectMany(x => x).ToArray());
    }
}
