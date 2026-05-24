using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Content.Analyzers.Honk;

/// <summary>
/// HONK0022: <c>HandsSystem.EnumerateHands()</c> returns the names of every
/// hand on the entity, not the unoccupied ones. Counting the sequence
/// (with <c>.Count()</c> or <c>.Any()</c>) almost always means the author
/// wanted "do I have a free hand," which is what <c>CountFreeHands()</c>
/// answers. The bug is silent: an entity holding an item still passes
/// "has any hand" gates. Scoped to fork files.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HonkEnumerateHandsCountAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Descriptor = new(
        id: "HONK0022",
        title: "EnumerateHands().Count()/.Any() counts every hand, including occupied",
        messageFormat: "EnumerateHands().{0}() counts every hand on the entity; use CountFreeHands() for free-hand checks, or add an explicit comment if total-hand count is intended",
        category: "Honk.Hands",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "HandsSystem.EnumerateHands returns hand names. The common .Count()/.Any() pattern conflates total hands with free hands and silently breaks gating logic.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var outer = (InvocationExpressionSyntax)context.Node;

        if (outer.Expression is not MemberAccessExpressionSyntax outerMember)
            return;

        var outerName = outerMember.Name.Identifier.ValueText;
        if (outerName != "Count" && outerName != "Any")
            return;

        if (outerMember.Expression is not InvocationExpressionSyntax inner)
            return;

        var path = outer.SyntaxTree.FilePath;
        if (!IsForkFile(path))
            return;

        var innerSymbol = context.SemanticModel.GetSymbolInfo(inner, context.CancellationToken).Symbol as IMethodSymbol;
        if (innerSymbol is null)
            return;

        if (innerSymbol.Name != "EnumerateHands")
            return;

        if (!IsHandsSystem(innerSymbol.ContainingType))
            return;

        context.ReportDiagnostic(Diagnostic.Create(Descriptor, outerMember.Name.GetLocation(), outerName));
    }

    private static bool IsHandsSystem(INamedTypeSymbol? type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.Name is "SharedHandsSystem" or "HandsSystem")
                return true;
        }
        return false;
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
