using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Content.Analyzers.Honk;

/// <summary>
/// HONK0015: fires when three or more <c>*.Honk.cs</c> files declare a
/// <c>partial</c> class for the same upstream target type. The fork's
/// documented escalation rule (see <c>CLAUDE.md</c> "Access Analyzer
/// Policy") is that two or three Honk partials is the ceiling; past that,
/// ownership of the subsystem has effectively migrated to the fork and the
/// right move is a HONK-marked <c>[Access(typeof(ForkSystem))]</c> on the
/// upstream component, not another setter file. The diagnostic reports the
/// third-and-subsequent partial declarations so each excess file gets
/// flagged until the author consolidates.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HonkPartialProliferationAnalyzer : DiagnosticAnalyzer
{
    private const int Threshold = 3;

    private static readonly DiagnosticDescriptor Descriptor = new(
        id: "HONK0015",
        title: "Too many Honk partials target the same type",
        messageFormat: "Target type '{0}' now has {1} Honk partials; consolidate into a HONK-marked [Access] addition on the upstream component",
        category: "Honk.Access",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Two or three *.Honk.cs partials per target is the escalation ceiling. Beyond that the fork is re-exporting the component through setter surface, which fragments ownership and makes rebases harder than one centralised [Access] allowlist line would.",
        customTags: new[] { WellKnownDiagnosticTags.CompilationEnd });

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var groups = new Dictionary<string, List<(string FilePath, Location Location)>>(StringComparer.Ordinal);
        var gate = new object();

        context.RegisterSyntaxNodeAction(ctx =>
        {
            var cls = (ClassDeclarationSyntax)ctx.Node;

            var path = cls.SyntaxTree.FilePath;
            if (!IsHonkPartialFile(path))
                return;

            if (!cls.Modifiers.Any(SyntaxKind.PartialKeyword))
                return;

            if (ctx.SemanticModel.GetDeclaredSymbol(cls, ctx.CancellationToken) is not INamedTypeSymbol symbol)
                return;

            var key = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var entry = (FilePath: NormalizePath(path), Location: cls.Identifier.GetLocation());

            lock (gate)
            {
                if (!groups.TryGetValue(key, out var list))
                {
                    list = new List<(string, Location)>();
                    groups[key] = list;
                }
                list.Add(entry);
            }
        }, SyntaxKind.ClassDeclaration);

        context.RegisterCompilationEndAction(ctx =>
        {
            foreach (var pair in groups)
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                var ordered = new List<(string FilePath, Location Location)>(pair.Value);
                ordered.Sort((a, b) => string.CompareOrdinal(a.FilePath, b.FilePath));

                var distinctCount = 0;
                foreach (var entry in ordered)
                {
                    if (!seen.Add(entry.FilePath))
                        continue;

                    distinctCount++;
                    if (distinctCount < Threshold)
                        continue;

                    ctx.ReportDiagnostic(Diagnostic.Create(
                        Descriptor,
                        entry.Location,
                        pair.Key,
                        distinctCount));
                }
            }
        });
    }

    private static bool IsHonkPartialFile(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return false;
        return NormalizePath(path!).EndsWith(".Honk.cs", StringComparison.Ordinal);
    }

    private static string NormalizePath(string path) => path.Replace('\\', '/');
}
