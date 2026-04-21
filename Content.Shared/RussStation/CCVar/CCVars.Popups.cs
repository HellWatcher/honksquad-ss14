using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    // Popup mirror routing (issue #578). Each CVar holds a route enum:
    //   0 = Disabled (float only, no mirror)
    //   1 = PopupTab (mirror into the Popup chat tab)
    //   2 = MainChat (mirror into the local chat tab as well)
    //
    // Server-originated popups carry no category and fall back to the Default CVar.
    // Categorized popups use their category's CVar.

    public static readonly CVarDef<int> PopupMirrorDefault =
        CVarDef.Create("honk.popup.mirror.default", 1, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<int> PopupMirrorFlavor =
        CVarDef.Create("honk.popup.mirror.flavor", 0, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<int> PopupMirrorCombat =
        CVarDef.Create("honk.popup.mirror.combat", 1, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<int> PopupMirrorMedical =
        CVarDef.Create("honk.popup.mirror.medical", 1, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<int> PopupMirrorEnvironmental =
        CVarDef.Create("honk.popup.mirror.environmental", 1, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<int> PopupMirrorInteraction =
        CVarDef.Create("honk.popup.mirror.interaction", 0, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<int> PopupMirrorSystem =
        CVarDef.Create("honk.popup.mirror.system", 1, CVar.CLIENTONLY | CVar.ARCHIVE);
}
