using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Content.Analyzers.Honk;

/// <summary>
/// HONK0030: a bare-statement call to <c>Resolve(...)</c> inside an
/// <see cref="EntitySystem"/> whose <see langword="bool"/> return value is
/// discarded. <c>EntitySystem.Resolve</c> returns <see langword="false"/> when
/// the component is missing; ignoring the bool and using the <c>ref</c>
/// parameter anyway is a latent NRE/throw. Scoped to fork files (path contains
/// <c>/RussStation/</c> or ends in <c>.Honk.cs</c>).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HonkDiscardedResolveAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Descriptor = new(
        id: "HONK0030",
        title: "Discarded Resolve() return value",
        messageFormat: "Resolve(...) return value is discarded; if the component is absent the ref parameter is left unusable and later access throws",
        category: "Honk.EntitySystem",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "EntitySystem.Resolve returns false when the component is missing; ignoring the bool and using the ref parameter anyway is a latent NRE/throw. Scope to Resolve only — bare-statement TryComp with an out var is an intentional idiom and must NOT be flagged.");

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
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (!IsForkFile(invocation.SyntaxTree.FilePath))
            return;

        var name = GetInvokedName(invocation);
        if (name != "Resolve")
            return;

        if (invocation.Parent is not ExpressionStatementSyntax)
            return;

        var containingType = context.ContainingSymbol?.ContainingType;
        if (containingType is null || !InheritsEntitySystem(containingType))
            return;

        context.ReportDiagnostic(Diagnostic.Create(Descriptor, invocation.GetLocation()));
    }

    private static bool IsForkFile(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        return path!.Replace('\\', '/').Contains("/RussStation/")
            || path.EndsWith(".Honk.cs", StringComparison.Ordinal);
    }

    private static string? GetInvokedName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            IdentifierNameSyntax id => id.Identifier.ValueText,
            GenericNameSyntax gn => gn.Identifier.ValueText,
            MemberAccessExpressionSyntax m => m.Name.Identifier.ValueText,
            _ => null,
        };
    }

    private static bool InheritsEntitySystem(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.Name == "EntitySystem" &&
                current.ContainingNamespace?.ToDisplayString() == "Robust.Shared.GameObjects")
            {
                return true;
            }
        }
        return false;
    }
}
