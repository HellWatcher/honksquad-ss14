using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Content.Analyzers.Honk;

/// <summary>
/// HONK0010: writing a field on a <c>[NetworkedComponent]</c> inside a
/// method that never calls a dirty-marking API (<c>Dirty</c>,
/// <c>DirtyField</c>, <c>DirtyFields</c>, <c>DirtyEntity</c>) is the single
/// most common desync source in content code. The server mutates state,
/// the client never receives a delta, and the bug only surfaces under
/// real latency. Scoped to fork files (path contains <c>/RussStation/</c>
/// or ends in <c>.Honk.cs</c>) so upstream drift is not this rule's problem.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HonkMissingDirtyAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Descriptor = new(
        id: "HONK0010",
        title: "Networked component write without Dirty()",
        messageFormat: "Write to '{0}' on networked component '{1}' without a Dirty/DirtyField/DirtyFields/DirtyEntity call in the same method",
        category: "Honk.Networking",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Networked components rely on Dirty() to replicate server writes. A mutation with no Dirty call reaches only the server; the client stays stale until some other path dirties the entity.");

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

        if (!IsForkFile(assignment.SyntaxTree.FilePath))
            return;

        if (assignment.Left is not MemberAccessExpressionSyntax memberAccess)
            return;

        var fieldSymbol = context.SemanticModel.GetSymbolInfo(memberAccess, context.CancellationToken).Symbol;
        if (fieldSymbol is not IFieldSymbol && fieldSymbol is not IPropertySymbol)
            return;

        var containingComponent = fieldSymbol.ContainingType;
        if (containingComponent is null || !HasNetworkedComponentAttribute(containingComponent))
            return;

        // Skip fields handled by the source-generated auto-state setter.
        if (HasAttribute(fieldSymbol, "AutoNetworkedFieldAttribute"))
            return;

        var methodBody = GetEnclosingMethodBody(assignment);
        if (methodBody is null)
            return;

        if (ContainsDirtyCall(methodBody))
            return;

        var fieldName = fieldSymbol.Name;
        var componentName = containingComponent.Name;
        context.ReportDiagnostic(Diagnostic.Create(Descriptor, assignment.GetLocation(), fieldName, componentName));
    }

    private static bool IsForkFile(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        return path!.Replace('\\', '/').Contains("/RussStation/")
            || path.EndsWith(".Honk.cs", StringComparison.Ordinal);
    }

    private static bool HasNetworkedComponentAttribute(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            foreach (var attr in current.GetAttributes())
            {
                if (attr.AttributeClass?.Name == "NetworkedComponentAttribute")
                    return true;
            }
        }
        return false;
    }

    private static bool HasAttribute(ISymbol symbol, string attributeName)
    {
        foreach (var attr in symbol.GetAttributes())
        {
            if (attr.AttributeClass?.Name == attributeName)
                return true;
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
                case AccessorDeclarationSyntax a:
                    return (SyntaxNode?)a.Body ?? a.ExpressionBody;
                case ConstructorDeclarationSyntax c:
                    return (SyntaxNode?)c.Body ?? c.ExpressionBody;
            }
        }
        return null;
    }

    private static bool ContainsDirtyCall(SyntaxNode body)
    {
        foreach (var invocation in body.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            var name = GetInvokedName(invocation);
            if (name is "Dirty" or "DirtyField" or "DirtyFields" or "DirtyEntity")
                return true;
        }
        return false;
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
}
