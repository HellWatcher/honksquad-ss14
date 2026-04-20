using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Content.Analyzers.Honk;

/// <summary>
/// HONK0007: forbids <c>IoCManager.Resolve&lt;T&gt;()</c> inside any type
/// deriving from <c>Robust.Shared.GameObjects.EntitySystem</c>. Systems are
/// constructed by the engine with DI available via <c>[Dependency]</c>
/// field injection. Reaching into the IoC container defeats lifetime
/// tracking and test substitution, and hides the system's dependency list.
/// Scoped to fork files (path contains <c>/RussStation/</c> or ends in
/// <c>.Honk.cs</c>) so upstream drift is not this rule's problem.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HonkEntitySystemIoCResolveAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Descriptor = new(
        id: "HONK0007",
        title: "IoCManager.Resolve inside EntitySystem",
        messageFormat: "'{0}' uses IoCManager.Resolve inside an EntitySystem; declare a [Dependency] field instead",
        category: "Honk.EntitySystem",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "EntitySystems receive dependencies via [Dependency] field injection. IoCManager.Resolve bypasses that, hiding the system's dependency list and breaking test substitution.");

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

        if (!IsIoCResolveCall(invocation))
            return;

        var containingType = context.ContainingSymbol?.ContainingType;
        if (containingType is null)
            return;

        if (!InheritsEntitySystem(containingType))
            return;

        context.ReportDiagnostic(Diagnostic.Create(Descriptor, invocation.GetLocation(), containingType.Name));
    }

    private static bool IsIoCResolveCall(InvocationExpressionSyntax invocation)
    {
        // Match IoCManager.Resolve<T>(...) and IoCManager.Instance.Resolve<T>(...).
        if (invocation.Expression is not MemberAccessExpressionSyntax member)
            return false;

        if (member.Name is not GenericNameSyntax generic)
            return false;

        if (generic.Identifier.ValueText != "Resolve")
            return false;

        return ReceiverIsIoCManager(member.Expression);
    }

    private static bool ReceiverIsIoCManager(ExpressionSyntax expr)
    {
        return expr switch
        {
            IdentifierNameSyntax id => id.Identifier.ValueText == "IoCManager",
            MemberAccessExpressionSyntax ma =>
                ma.Name.Identifier.ValueText == "Instance" && ReceiverIsIoCManager(ma.Expression)
                || ma.Name.Identifier.ValueText == "IoCManager",
            _ => false,
        };
    }

    private static bool IsForkFile(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        return path!.Replace('\\', '/').Contains("/RussStation/")
            || path.EndsWith(".Honk.cs", StringComparison.Ordinal);
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
