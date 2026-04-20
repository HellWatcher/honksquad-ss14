using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Content.Analyzers.Honk;

/// <summary>
/// HONK0003: a direct write to an upstream component field/property from a
/// fork file under `*/RussStation/**` drifts silently past rebase. Writes
/// that need to happen should either go through a HONK-marked
/// `[Access(typeof(ForkSystem))]` friend on the upstream type, a partial
/// `*.Honk.cs` setter on the owning upstream system, or an inline
/// `#pragma warning disable HONK0003` with a reason.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HonkUpstreamFieldWriteAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Descriptor = new(
        id: "HONK0003",
        title: "Direct write to upstream component from fork file",
        messageFormat: "Direct write to upstream member '{0}.{1}' from a fork file; add a HONK-marked [Access] friend or move to a .Honk.cs partial instead",
        category: "Honk.Access",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The fork leans on partial-class setters and [Access] friends to keep drift visible to the rebase auditor. A bare `comp.Field = value` in a RussStation file hides that drift and is hard to preserve through upstream syncs.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.SimpleAssignmentExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;

        var path = assignment.SyntaxTree.FilePath;
        if (!IsForkFile(path) || IsHonkPartialFile(path))
            return;

        if (assignment.Left is not MemberAccessExpressionSyntax member)
            return;

        var symbol = context.SemanticModel.GetSymbolInfo(member, context.CancellationToken).Symbol;
        if (symbol is null)
            return;

        if (symbol is not IFieldSymbol && symbol is not IPropertySymbol)
            return;

        var containing = symbol.ContainingType;
        if (containing is null)
            return;

        if (!containing.Name.EndsWith("Component", StringComparison.Ordinal))
            return;

        if (IsForkType(containing))
            return;

        if (!IsContentType(containing))
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            Descriptor,
            member.Name.GetLocation(),
            containing.Name,
            symbol.Name));
    }

    private static bool IsForkFile(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return false;
        return path!.Replace('\\', '/').Contains("/RussStation/");
    }

    private static bool IsHonkPartialFile(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return false;
        return path!.Replace('\\', '/').EndsWith(".Honk.cs", StringComparison.Ordinal);
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

    private static bool IsContentType(INamedTypeSymbol symbol)
    {
        for (var ns = symbol.ContainingNamespace; ns is { IsGlobalNamespace: false }; ns = ns.ContainingNamespace)
        {
            if (ns.Name == "Content" && ns.ContainingNamespace is { IsGlobalNamespace: true })
                return true;
        }
        return false;
    }
}
