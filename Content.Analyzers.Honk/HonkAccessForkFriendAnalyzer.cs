using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Content.Analyzers.Honk;

/// <summary>
/// HONK0005: an `[Access(typeof(X))]` attribute where X is a fork-owned
/// system (namespace contains `RussStation`) must sit inside a `// HONK`
/// block when the containing file is upstream. Fork-owned files (path
/// contains `/RussStation/` or filename ends in `.Honk.cs`) are exempt,
/// the whole file is fork drift there.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HonkAccessForkFriendAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Descriptor = new(
        id: "HONK0005",
        title: "[Access] typeof fork system must sit inside a HONK block",
        messageFormat: "[Access] references fork system '{0}' from an upstream file; wrap the attribute in // HONK START / // HONK END so rebase can see the fork-specific friend",
        category: "Honk.Access",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Adding a fork system as an [Access] friend on an upstream component drifts from upstream. Wrap the attribute in a HONK block so the rebase auditor can find and preserve it.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.Attribute);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var attribute = (AttributeSyntax)context.Node;
        if (!IsAccessAttributeName(attribute.Name))
            return;

        if (IsForkFile(attribute.SyntaxTree.FilePath))
            return;

        var args = attribute.ArgumentList?.Arguments;
        if (args is null)
            return;

        foreach (var arg in args.Value)
        {
            if (arg.Expression is not TypeOfExpressionSyntax typeofExpr)
                continue;

            if (context.SemanticModel.GetSymbolInfo(typeofExpr.Type, context.CancellationToken).Symbol is not INamedTypeSymbol symbol)
                continue;

            if (!IsForkType(symbol))
                continue;

            var blocks = HonkMarkerBlocks.Find(context.Node.SyntaxTree.GetText(context.CancellationToken));
            if (HonkMarkerBlocks.Contains(blocks, attribute.Span))
                return;

            context.ReportDiagnostic(Diagnostic.Create(Descriptor, attribute.GetLocation(), symbol.Name));
            return;
        }
    }

    private static bool IsAccessAttributeName(NameSyntax name)
    {
        var simple = name switch
        {
            SimpleNameSyntax s => s.Identifier.ValueText,
            QualifiedNameSyntax q => q.Right.Identifier.ValueText,
            _ => null,
        };
        return simple is "Access" or "AccessAttribute";
    }

    private static bool IsForkFile(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return false;
        var norm = path!.Replace('\\', '/');
        return norm.Contains("/RussStation/") || norm.EndsWith(".Honk.cs", StringComparison.Ordinal);
    }

    private static bool IsForkType(INamedTypeSymbol symbol)
    {
        for (var ns = symbol.ContainingNamespace; ns is { IsGlobalNamespace: false }; ns = ns.ContainingNamespace)
        {
            if (ns.Name == "RussStation")
                return true;
        }
        return false;
    }
}
