using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Content.Analyzers.Honk;

/// <summary>
/// HONK0032: a per-tick <c>TryComp&lt;T&gt;</c> / <c>HasComp&lt;T&gt;</c> /
/// <c>Comp&lt;T&gt;</c> inside an <c>Update</c> / <c>FrameUpdate</c> query loop
/// (a <c>while (enumerator.MoveNext())</c> body) does a hashtable lookup per
/// entity per tick. The SS14-idiomatic faster path is to cache a
/// <c>GetEntityQuery&lt;T&gt;()</c> resolver in a field and use it inside the loop.
/// Scoped to fork files (path contains <c>/RussStation/</c> or ends in
/// <c>.Honk.cs</c>) so upstream drift is not this rule's problem.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HonkUncachedComponentLookupInTickLoopAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Descriptor = new(
        id: "HONK0032",
        title: "Per-tick component lookup could use a cached EntityQuery",
        messageFormat: "{0}<{1}> runs a dictionary lookup every tick inside an Update query loop; cache a GetEntityQuery<{1}>() resolver in a field and use it here",
        category: "Honk.Perf",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A per-tick TryComp/HasComp/Comp in an Update enumerator loop does a hashtable lookup per entity per tick; a cached EntityQuery<T> resolver is the SS14-idiomatic faster path.");

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

        var methodName = invokedName.Identifier.ValueText;
        if (methodName != "TryComp" && methodName != "HasComp" && methodName != "Comp")
            return;

        if (invokedName.TypeArgumentList.Arguments.Count == 0)
            return;

        if (!IsInsideMoveNextLoop(invocation))
            return;

        if (!IsInsideTickUpdate(invocation))
            return;

        var typeArgText = invokedName.TypeArgumentList.Arguments[0].ToString();
        context.ReportDiagnostic(Diagnostic.Create(Descriptor, invocation.GetLocation(), methodName, typeArgText));
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

    private static bool IsInsideMoveNextLoop(SyntaxNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (current is WhileStatementSyntax whileStmt &&
                whileStmt.Condition.ToString().Contains("MoveNext"))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsInsideTickUpdate(SyntaxNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (current is MethodDeclarationSyntax method)
            {
                var name = method.Identifier.ValueText;
                if (name != "Update" && name != "FrameUpdate")
                    return false;

                return method.Modifiers.IndexOf(SyntaxKind.OverrideKeyword) >= 0;
            }
        }
        return false;
    }
}
