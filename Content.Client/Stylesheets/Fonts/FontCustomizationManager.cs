using System.IO;
using System.Linq;
using Content.Client.Resources;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Log;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Stylesheets.Fonts;

/// <summary>
///     Manages font discovery (built-in + user-provided) and triggers stylesheet
///     rebuilds when the player changes their font settings.
/// </summary>
public sealed class FontCustomizationManager
{
    [Dependency] private readonly IResourceManager _resManager = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IResourceCache _resCache = default!;
    [Dependency] private readonly FontTagHijackHolder _fontHijack = default!;

    private ISawmill _sawmill = default!;
    private MemoryContentRoot? _userFontRoot;

    private static readonly ResPath UserFontsDir = new("/fonts");
    private static readonly ResPath ContentFontsDir = new("/Fonts");

    // Font prototype IDs used by the rich text system
    private static readonly string[] HijackedPrototypes =
        ["Default", "DefaultBold", "DefaultItalic", "DefaultBoldItalic"];

    /// <summary>
    ///     All discovered font families, keyed by family name.
    /// </summary>
    public Dictionary<string, FontFamilyInfo> AvailableFonts { get; } = new();

    /// <summary>
    ///     Raised when fonts should be reloaded (CVar changed or user fonts rescanned).
    /// </summary>
    public event Action? FontsChanged;

    public string CurrentFamily => _cfg.GetCVar(CCVars.UIFontFamily);
    public int CurrentSize => _cfg.GetCVar(CCVars.UIFontSize);

    public void Initialize()
    {
        _sawmill = Logger.GetSawmill("font");

        try
        {
            _resManager.UserData.CreateDir(UserFontsDir);
            LoadUserFonts();
        }
        catch (NotImplementedException)
        {
            // VirtualWritableDirProvider in test/linter environments doesn't support Find()
            _sawmill.Debug("User font loading skipped (virtual filesystem)");
        }

        DiscoverFonts();
        SetupFontHijack();

        _cfg.OnValueChanged(CCVars.UIFontFamily, _ =>
        {
            FontsChanged?.Invoke();
            _fontHijack.HijackUpdated();
        });
        _cfg.OnValueChanged(CCVars.UIFontSize, _ =>
        {
            FontsChanged?.Invoke();
            _fontHijack.HijackUpdated();
        });
    }

    /// <summary>
    ///     Creates a Font for the current custom font at the given size and kind.
    ///     Falls back to NotoSans if the custom font can't be loaded.
    /// </summary>
    public Font GetCurrentFont(int size, FontKind kind = FontKind.Regular)
    {
        var template = GetFontPathTemplate();
        var kinds = GetAvailableKinds();

        // Fall back to regular if requested kind isn't available
        if (!kinds.Contains(kind))
            kind = kind == FontKind.BoldItalic && kinds.Contains(FontKind.Bold) ? FontKind.Bold : FontKind.Regular;

        var kindStr = kind.AsFileName();
        var path = string.Format(template, kindStr);

        try
        {
            return _resCache.GetFont(path, size);
        }
        catch
        {
            _sawmill.Warning($"Failed to load font at '{path}', falling back to NotoSans");
            return _resCache.GetFont($"/Fonts/NotoSans/NotoSans-{kindStr}.ttf", size);
        }
    }

    private void SetupFontHijack()
    {
        _fontHijack.Hijack = (protoId, size) =>
        {
            // Only hijack if using a non-default font
            if (CurrentFamily == "NotoSans")
                return null;

            var id = protoId.Id;
            if (!HijackedPrototypes.Contains(id))
                return null;

            var kind = id switch
            {
                "DefaultBold" => FontKind.Bold,
                "DefaultItalic" => FontKind.Italic,
                "DefaultBoldItalic" => FontKind.BoldItalic,
                _ => FontKind.Regular,
            };

            return GetCurrentFont(size, kind);
        };
    }

    /// <summary>
    ///     Opens the user fonts folder in the OS file manager.
    /// </summary>
    public void OpenUserFontsFolder()
    {
        _resManager.UserData.OpenOsWindow(UserFontsDir);
    }

    /// <summary>
    ///     Rescans for user fonts and rebuilds the available font list.
    /// </summary>
    public void RescanUserFonts()
    {
        LoadUserFonts();
        DiscoverFonts();
        FontsChanged?.Invoke();
        _fontHijack.HijackUpdated();
    }

    /// <summary>
    ///     Gets the font path template for the current (or specified) family,
    ///     compatible with <see cref="NotoFontFamilyStack"/>.
    /// </summary>
    public string GetFontPathTemplate(string? familyName = null)
    {
        familyName ??= CurrentFamily;

        if (AvailableFonts.TryGetValue(familyName, out var info))
            return info.PathTemplate;

        // Fallback to NotoSans
        _sawmill.Warning($"Font family '{familyName}' not found, falling back to NotoSans");
        return "/Fonts/NotoSans/NotoSans-{0}.ttf";
    }

    /// <summary>
    ///     Gets available font kinds for the current (or specified) family.
    /// </summary>
    public HashSet<FontKind> GetAvailableKinds(string? familyName = null)
    {
        familyName ??= CurrentFamily;

        if (AvailableFonts.TryGetValue(familyName, out var info))
            return info.AvailableKinds;

        return [FontKind.Regular, FontKind.Bold, FontKind.Italic, FontKind.BoldItalic];
    }

    /// <summary>
    ///     Read user font files from UserData/fonts/ and mount them as a content root
    ///     so they're accessible through the normal resource cache.
    /// </summary>
    private void LoadUserFonts()
    {
        var isFirstLoad = _userFontRoot == null;

        if (_userFontRoot != null)
            _userFontRoot.Clear();
        else
            _userFontRoot = new MemoryContentRoot();

        var (files, _) = _resManager.UserData.Find("fonts/*");
        foreach (var file in files)
        {
            var ext = file.Extension;
            if (ext != "ttf" && ext != "otf")
                continue;

            try
            {
                using var stream = _resManager.UserData.OpenRead(file);
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                var bytes = ms.ToArray();

                // Map UserData/fonts/X to content path /Fonts/UserFonts/X
                var relativePath = file.RelativeTo(new ResPath("/fonts/"));
                var contentPath = new ResPath("Fonts/UserFonts") / relativePath;

                _userFontRoot.AddOrUpdateFile(contentPath, bytes);
                _sawmill.Debug($"Loaded user font: {file} -> /{contentPath}");
            }
            catch (Exception e)
            {
                _sawmill.Error($"Failed to load user font {file}: {e.Message}");
            }
        }

        // Only mount the root once; subsequent rescans just update its contents
        if (isFirstLoad)
            _resManager.AddRoot(new ResPath("/"), _userFontRoot);
    }

    /// <summary>
    ///     Discovers all available font families from content roots and user fonts.
    /// </summary>
    private void DiscoverFonts()
    {
        AvailableFonts.Clear();

        // Discover built-in fonts
        DiscoverFontsInPath(ContentFontsDir, false);

        // Discover user fonts (mounted under /Fonts/UserFonts/)
        DiscoverFontsInPath(new ResPath("/Fonts/UserFonts"), true);
    }

    private void DiscoverFontsInPath(ResPath basePath, bool isUserFont)
    {
        IEnumerable<ResPath> fontFiles;
        try
        {
            fontFiles = _resManager.ContentFindFiles(basePath)
                .Where(p => p.Extension is "ttf" or "otf")
                .ToList();
        }
        catch
        {
            return;
        }

        foreach (var fontFile in fontFiles)
        {
            var fileName = fontFile.Filename;
            var dirName = fontFile.Directory.Filename;

            // Skip symbol/emoji fonts and the UserFonts subdirectory during built-in scan
            if (fileName.Contains("Symbol", StringComparison.OrdinalIgnoreCase) ||
                fileName.Contains("Emoji", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!isUserFont && fontFile.ToString().Contains("UserFonts"))
                continue;

            // Detect family name and kind from filename
            // Convention: FamilyName-Kind.ttf (e.g., NotoSans-Bold.ttf)
            var nameWithoutExt = fontFile.FilenameWithoutExtension;
            string familyName;
            var kind = FontKind.Regular;

            var dashIndex = nameWithoutExt.LastIndexOf('-');
            if (dashIndex > 0)
            {
                familyName = nameWithoutExt[..dashIndex];
                var kindStr = nameWithoutExt[(dashIndex + 1)..];
                kind = ParseFontKind(kindStr);
            }
            else
            {
                // No dash, treat as regular variant; use directory name if it looks like a family
                familyName = dirName != basePath.Filename ? dirName : nameWithoutExt;
            }

            // Skip if this is a sub-font of NotoSans that isn't the main family
            // (e.g., NotoSansSymbols is separate from NotoSans)
            if (familyName.StartsWith("NotoSans", StringComparison.Ordinal) &&
                familyName != "NotoSans" &&
                familyName != "NotoSansDisplay")
            {
                continue;
            }

            if (!AvailableFonts.TryGetValue(familyName, out var info))
            {
                string template;
                if (dashIndex > 0)
                {
                    // Multi-variant family: FamilyName-{Kind}.ext
                    var dirPath = fontFile.Directory;
                    template = $"{dirPath}/{familyName}-{{0}}.{fontFile.Extension}";
                }
                else
                {
                    // Single-file font: always use the same file regardless of kind
                    template = fontFile.ToString();
                }

                info = new FontFamilyInfo(familyName, template, isUserFont);
                AvailableFonts[familyName] = info;
            }

            info.AvailableKinds.Add(kind);
        }
    }

    private static FontKind ParseFontKind(string kindStr)
    {
        return kindStr.ToLowerInvariant() switch
        {
            "regular" => FontKind.Regular,
            "bold" => FontKind.Bold,
            "italic" => FontKind.Italic,
            "bolditalic" => FontKind.BoldItalic,
            _ => FontKind.Regular,
        };
    }
}

/// <summary>
///     Information about a discovered font family.
/// </summary>
public sealed class FontFamilyInfo
{
    public string Name { get; }

    /// <summary>
    ///     Path template with {0} placeholder for font kind (Regular, Bold, etc.)
    /// </summary>
    public string PathTemplate { get; }

    public bool IsUserFont { get; }
    public HashSet<FontKind> AvailableKinds { get; } = new();

    public FontFamilyInfo(string name, string pathTemplate, bool isUserFont)
    {
        Name = name;
        PathTemplate = pathTemplate;
        IsUserFont = isUserFont;
    }
}
