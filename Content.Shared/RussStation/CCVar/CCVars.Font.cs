using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// The font family used for UI text. "NotoSans" is the default.
    /// User-provided fonts can be placed in the UserData/fonts/ directory.
    /// </summary>
    public static readonly CVarDef<string> UIFontFamily =
        CVarDef.Create("ui.font_family", "NotoSans", CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Base font size for UI text.
    /// </summary>
    public static readonly CVarDef<int> UIFontSize =
        CVarDef.Create("ui.font_size", 12, CVar.CLIENTONLY | CVar.ARCHIVE);
}
