using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Client.Stylesheets.Fonts;
using Content.Client.Stylesheets.Stylesheets;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Reflection;

namespace Content.Client.Stylesheets
{
    public sealed class StylesheetManager : IStylesheetManager
    {
        [Dependency] private readonly ILogManager _logManager = default!;
        [Dependency] private readonly IUserInterfaceManager _userInterfaceManager = default!;
        [Dependency] private readonly IReflectionManager _reflection = default!;

        [Dependency]
        private readonly IResourceCache
            _resCache = default!; // TODO: REMOVE (obsolete; used to construct StyleNano/StyleSpace)

        public Stylesheet SheetNanotrasen { get; private set; } = default!;
        public Stylesheet SheetSystem { get; private set; } = default!;

        [Obsolete("Update to use SheetNanotrasen instead")]
        public Stylesheet SheetNano { get; private set; } = default!;

        [Obsolete("Update to use SheetSystem instead")]
        public Stylesheet SheetSpace { get; private set; } = default!;

        private Dictionary<string, Stylesheet> Stylesheets { get; set; } = default!;

        // HONK START - Font customization
        public FontCustomizationManager FontManager { get; private set; } = default!;
        // HONK END

        public bool TryGetStylesheet(string name, [MaybeNullWhen(false)] out Stylesheet stylesheet)
        {
            return Stylesheets.TryGetValue(name, out stylesheet);
        }

        public HashSet<Type> UnusedSheetlets { get; private set; } = [];

        public void Initialize()
        {
            var sawmill = _logManager.GetSawmill("style");
            sawmill.Debug("Initializing Stylesheets...");
            var sw = Stopwatch.StartNew();

            // HONK START - Font customization
            FontManager = new FontCustomizationManager();
            IoCManager.InjectDependencies(FontManager);
            FontManager.Initialize();
            FontManager.FontsChanged += RebuildStylesheets;
            // HONK END

            // add all sheetlets to the hashset
            var tys = _reflection.FindTypesWithAttribute<CommonSheetletAttribute>();
            UnusedSheetlets = [..tys];

            Stylesheets = new Dictionary<string, Stylesheet>();
            SheetNanotrasen = Init(new NanotrasenStylesheet(new BaseStylesheet.NoConfig(), this));
            SheetSystem = Init(new SystemStylesheet(new BaseStylesheet.NoConfig(), this));
            SheetNano = new StyleNano(_resCache).Stylesheet; // TODO: REMOVE (obsolete)
            SheetSpace = new StyleSpace(_resCache).Stylesheet; // TODO: REMOVE (obsolete)

            _userInterfaceManager.Stylesheet = SheetNanotrasen;

            // warn about unused sheetlets
            if (UnusedSheetlets.Count > 0)
            {
                var sheetlets = UnusedSheetlets.AsEnumerable()
                    .Take(5)
                    .Select(t => t.FullName ?? "<could not get FullName>")
                    .ToArray();
                sawmill.Error($"There are unloaded sheetlets: {string.Join(", ", sheetlets)}");
            }

            sawmill.Debug($"Initialized {_styleRuleCount} style rules in {sw.Elapsed}");
        }

        // HONK START - Font customization
        /// <summary>
        ///     Rebuilds all stylesheets with current font settings and reassigns to the UI manager.
        /// </summary>
        private void RebuildStylesheets()
        {
            var sawmill = _logManager.GetSawmill("style");
            sawmill.Debug("Rebuilding stylesheets for font change...");
            var sw = Stopwatch.StartNew();

            var tys = _reflection.FindTypesWithAttribute<CommonSheetletAttribute>();
            UnusedSheetlets = [..tys];

            Stylesheets.Clear();
            _styleRuleCount = 0;

            SheetNanotrasen = Init(new NanotrasenStylesheet(new BaseStylesheet.NoConfig(), this));
            SheetSystem = Init(new SystemStylesheet(new BaseStylesheet.NoConfig(), this));
            SheetNano = new StyleNano(_resCache).Stylesheet;
            SheetSpace = new StyleSpace(_resCache).Stylesheet;

            _userInterfaceManager.Stylesheet = SheetNanotrasen;

            sawmill.Debug($"Rebuilt {_styleRuleCount} style rules in {sw.Elapsed}");
        }
        // HONK END

        private int _styleRuleCount;

        private Stylesheet Init(BaseStylesheet baseSheet)
        {
            Stylesheets.Add(baseSheet.StylesheetName, baseSheet.Stylesheet);
            _styleRuleCount += baseSheet.Stylesheet.Rules.Count;
            return baseSheet.Stylesheet;
        }
    }
}
