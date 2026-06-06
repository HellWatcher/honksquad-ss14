namespace Content.Server.RussStation.Messenger;

/// <summary>
/// Classifies messenger addresses by antag category and derives the address prefix for a PDA name.
/// The keyword/prefix map is injectable so it can be exercised (or overridden) in isolation.
/// </summary>
public sealed class AntagAddressFilter
{
    /// <summary>
    /// Default address prefix used for station crew (Nanotrasen) and anything that isn't an antag.
    /// </summary>
    public const string CrewAddressPrefix = "NT";

    /// <summary>
    /// Default keyword -> prefix map for antag categories. A PDA whose name contains one of the
    /// keywords is assigned that prefix, and any address starting with one of the prefixes is
    /// treated as an antag address.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> DefaultAntagPrefixes = new Dictionary<string, string>
    {
        { "syndicate", "SY" },
        { "ninja", "NJ" },
        { "pirate", "PR" },
        { "wizard", "WZ" },
        { "CBURN", "CB" },
    };

    /// <summary>
    /// Shared default instance backed by <see cref="DefaultAntagPrefixes"/> and <see cref="CrewAddressPrefix"/>.
    /// </summary>
    public static readonly AntagAddressFilter Default = new();

    private readonly IReadOnlyDictionary<string, string> _antagPrefixes;
    private readonly string _crewPrefix;

    public AntagAddressFilter(
        IReadOnlyDictionary<string, string>? antagPrefixes = null,
        string crewPrefix = CrewAddressPrefix)
    {
        _antagPrefixes = antagPrefixes ?? DefaultAntagPrefixes;
        _crewPrefix = crewPrefix;
    }

    /// <summary>
    /// The prefix handed to non-antag (station crew) cartridges.
    /// </summary>
    public string CrewPrefix => _crewPrefix;

    /// <summary>
    /// Pick the address prefix for a PDA based on keywords in its name. Antag PDAs (Syndicate,
    /// ninja, ...) get their category prefix; everything else gets the crew prefix.
    /// </summary>
    public string GetAddressPrefix(string pdaName)
    {
        foreach (var (keyword, prefix) in _antagPrefixes)
        {
            if (pdaName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                return prefix;
        }

        return _crewPrefix;
    }

    /// <summary>
    /// True if the address belongs to an antag category (i.e. starts with one of the antag prefixes).
    /// </summary>
    public bool IsAntagAddress(string address)
    {
        foreach (var (_, prefix) in _antagPrefixes)
        {
            if (address.StartsWith(prefix))
                return true;
        }

        return false;
    }
}
