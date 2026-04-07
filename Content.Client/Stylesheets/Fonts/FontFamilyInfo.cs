namespace Content.Client.Stylesheets.Fonts;

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
