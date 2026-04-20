using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Content.Analyzers.Honk;

/// <summary>
/// HONK0011: <c>Comp&lt;T&gt;(args.Target)</c> / <c>Comp&lt;T&gt;(args.User)</c>
/// / <c>Comp&lt;T&gt;(args.OtherEntity)</c> in an <see cref="EntitySystem"/>
/// handler with no preceding <c>HasComp&lt;T&gt;</c> / <c>TryComp&lt;T&gt;</c>
/// guard in the same method body. <c>Comp&lt;T&gt;</c> throws on miss;
/// server-side that kills the handler for the rest of the tick.
/// Scoped to fork files (path contains <c>/RussStation/</c> or ends in
/// <c>.Honk.cs</c>) so upstream drift is not this rule's problem.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HonkUnguardedCompAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Descriptor = new(
        id: "HONK0011",
        title: "Unguarded Comp<T>() on args.*",
        messageFormat: "Comp<{0}>({1}) has no HasComp<{0}>/TryComp<{0}> guard in the enclosing method; a missing component throws mid-tick",
        category: "Honk.EntitySystem",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Comp<T> throws when the component is absent. On args.Target / args.User / args.OtherEntity the absence is routine, and the throw wipes out the rest of the handler. Pair with HasComp<T> or TryComp<T> first.");

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

        var invokedName = GetGenericName(invocation);
        if (invokedName is null || invokedName.Identifier.ValueText != "Comp")
            return;

        if (invocation.ArgumentList.Arguments.Count < 1)
            return;

        var uidArg = invocation.ArgumentList.Arguments[0].Expression;
        if (!IsArgsMemberAccess(uidArg, out var uidText))
            return;

        var containingType = context.ContainingSymbol?.ContainingType;
        if (containingType is null || !InheritsEntitySystem(containingType))
            return;

        var methodBody = GetEnclosingMethodBody(invocation);
        if (methodBody is null)
            return;

        var typeArgText = invokedName.TypeArgumentList.Arguments[0].ToString();
        if (HasGuardCall(methodBody, typeArgText))
            return;

        context.ReportDiagnostic(Diagnostic.Create(Descriptor, invocation.GetLocation(), typeArgText, uidText));
    }

    private static bool IsForkFile(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        return path!.Replace('\\', '/').Contains("/RussStation/")
            || path.EndsWith(".Honk.cs", StringComparison.Ordinal);
    }

    private static GenericNameSyntax? GetGenericName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            GenericNameSyntax gn => gn,
            MemberAccessExpressionSyntax m => m.Name as GenericNameSyntax,
            _ => null,
        };
    }

    private static bool IsArgsMemberAccess(ExpressionSyntax expr, out string text)
    {
        text = string.Empty;
        if (expr is not MemberAccessExpressionSyntax member)
            return false;

        if (member.Expression is not IdentifierNameSyntax root || root.Identifier.ValueText != "args")
            return false;

        var name = member.Name.Identifier.ValueText;
        if (name != "Target" && name != "User" && name != "OtherEntity")
            return false;

        text = $"args.{name}";
        return true;
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

    private static SyntaxNode? GetEnclosingMethodBody(SyntaxNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            switch (current)
            {
                case MethodDeclarationSyntax m:
                    return (SyntaxNode?)m.Body ?? m.ExpressionBody;
                case LocalFunctionStatementSyntax lf:
                    return (SyntaxNode?)lf.Body ?? lf.ExpressionBody;
            }
        }
        return null;
    }

    private static bool HasGuardCall(SyntaxNode body, string typeArgText)
    {
        foreach (var inv in body.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            var generic = GetGenericName(inv);
            if (generic is null)
                continue;
            var name = generic.Identifier.ValueText;
            if (name != "HasComp" && name != "TryComp")
                continue;
            if (generic.TypeArgumentList.Arguments.Count == 0)
                continue;
            if (generic.TypeArgumentList.Arguments[0].ToString() == typeArgText)
                return true;
        }
        return false;
    }
}
