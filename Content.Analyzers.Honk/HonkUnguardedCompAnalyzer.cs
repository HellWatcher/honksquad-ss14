using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Content.Analyzers.Honk;

/// <summary>
/// HONK0011: <c>Comp&lt;T&gt;(receiver)</c> / <c>GetComponent&lt;T&gt;(receiver)</c>
/// for any receiver expression in an <see cref="EntitySystem"/> handler with no
/// preceding <c>HasComp&lt;T&gt;</c> / <c>TryComp&lt;T&gt;</c> guard in the same
/// method body. <c>Comp&lt;T&gt;</c> / <c>GetComponent&lt;T&gt;</c> throws on
/// miss; server-side that kills the handler for the rest of the tick.
/// Type arguments that always exist (<c>MetaDataComponent</c>,
/// <c>TransformComponent</c>) are whitelisted and never reported.
/// Scoped to fork files (path contains <c>/RussStation/</c> or ends in
/// <c>.Honk.cs</c>) so upstream drift is not this rule's problem.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HonkUnguardedCompAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Descriptor = new(
        id: "HONK0011",
        title: "Unguarded Comp<T>()/GetComponent<T>()",
        messageFormat: "Comp<{0}>({1}) has no HasComp<{0}>/TryComp<{0}> guard in the enclosing method; a missing component throws mid-tick",
        category: "Honk.EntitySystem",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Comp<T> / GetComponent<T> throw when the component is absent, and the throw wipes out the rest of the handler. For any receiver whose component is not guaranteed present, pair with HasComp<T> or TryComp<T> first. MetaDataComponent and TransformComponent always exist and are whitelisted.");

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
        if (invokedName is null)
            return;

        var name = invokedName.Identifier.ValueText;
        if (name != "Comp" && name != "GetComponent")
            return;

        if (invocation.ArgumentList.Arguments.Count < 1)
            return;

        if (invokedName.TypeArgumentList.Arguments.Count == 0)
            return;

        var uidArg = invocation.ArgumentList.Arguments[0].Expression;
        var uidText = GetReceiverText(uidArg);

        var containingType = context.ContainingSymbol?.ContainingType;
        if (containingType is null || !InheritsEntitySystem(containingType))
            return;

        var methodBody = GetEnclosingMethodBody(invocation);
        if (methodBody is null)
            return;

        var typeArgText = invokedName.TypeArgumentList.Arguments[0].ToString();
        if (typeArgText is "MetaDataComponent" or "TransformComponent")
            return;

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

    private static string GetReceiverText(ExpressionSyntax expr)
    {
        // Keep the args.Target / args.User / args.OtherEntity wording verbatim so
        // existing messages still read naturally; otherwise fall back to a short
        // textual form of whatever the receiver expression is.
        if (expr is MemberAccessExpressionSyntax member &&
            member.Expression is IdentifierNameSyntax root &&
            root.Identifier.ValueText == "args")
        {
            var memberName = member.Name.Identifier.ValueText;
            if (memberName is "Target" or "User" or "OtherEntity")
                return $"args.{memberName}";
        }

        return expr.ToString().Trim();
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
