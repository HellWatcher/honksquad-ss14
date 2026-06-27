using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Content.Analyzers.Honk;

/// <summary>
/// HONK0026: fires when the same typed prototype id literal -- a
/// <c>ProtoId&lt;T&gt;</c> initialised from a string literal -- is declared in
/// two or more fork files. Each copy is an independent magic string, so when
/// the upstream prototype id is renamed only the copies someone happens to
/// remember get updated and the rest silently drift to a dangling reference.
/// The fix is to extract one shared constant (e.g. a fork-owned
/// <c>EconomyConstants</c> field) and reference it everywhere. Scoped to fork
/// files; an upstream copy of the same literal is not counted because rebases
/// own that surface.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HonkDuplicateProtoIdConstantAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Descriptor = new(
        id: "HONK0026",
        title: "Duplicated ProtoId<T> constant across fork files",
        messageFormat: "ProtoId<{0}> = \"{1}\" is declared in {2} fork files; extract one shared constant to avoid drift",
        category: "Honk.Drift",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "When the same typed prototype id literal is redeclared in multiple fork files, an upstream id change updates only some copies and the rest drift to a dangling reference. Extract a single shared constant.",
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
        var groups = new ConcurrentDictionary<(string TypeArg, string Literal), ConcurrentBag<Location>>();

        context.RegisterSymbolAction(symCtx =>
        {
            var field = (IFieldSymbol)symCtx.Symbol;

            var location = field.Locations.Length > 0 ? field.Locations[0] : null;
            var path = location?.SourceTree?.FilePath;
            if (!IsForkFile(path))
                return;

            if (field.Type is not INamedTypeSymbol named
                || !named.IsGenericType
                || named.Name != "ProtoId"
                || named.TypeArguments.Length != 1)
            {
                return;
            }

            var typeArg = named.TypeArguments[0].Name;
            if (string.IsNullOrEmpty(typeArg))
                return;

            var literal = GetStringLiteral(field, symCtx.CancellationToken);
            if (literal is null)
                return;

            groups
                .GetOrAdd((typeArg, literal), _ => new ConcurrentBag<Location>())
                .Add(location!);
        }, SymbolKind.Field);

        context.RegisterCompilationEndAction(endCtx =>
        {
            foreach (var pair in groups)
            {
                var locations = pair.Value;
                var count = locations.Count;
                if (count < 2)
                    continue;

                foreach (var location in locations)
                {
                    endCtx.ReportDiagnostic(Diagnostic.Create(
                        Descriptor,
                        location,
                        pair.Key.TypeArg,
                        pair.Key.Literal,
                        count));
                }
            }
        });
    }

    private static string? GetStringLiteral(IFieldSymbol field, System.Threading.CancellationToken cancellationToken)
    {
        // Prefer the resolved constant value for `const` fields.
        if (field.IsConst && field.ConstantValue is string constValue)
            return constValue;

        // Otherwise inspect the declaring syntax's initializer for a string literal.
        foreach (var reference in field.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax(cancellationToken) is not VariableDeclaratorSyntax declarator)
                continue;

            if (declarator.Initializer?.Value is LiteralExpressionSyntax literal
                && literal.Token.Value is string value)
            {
                return value;
            }
        }

        return null;
    }

    private static bool IsForkFile(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        var normalized = path!.Replace('\\', '/');
        return normalized.Contains("/RussStation/")
            || normalized.EndsWith(".Honk.cs", StringComparison.Ordinal);
    }
}
