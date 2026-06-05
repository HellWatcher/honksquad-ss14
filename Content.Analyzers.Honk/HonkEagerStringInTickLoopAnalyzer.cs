using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Content.Analyzers.Honk;

/// <summary>
/// HONK0027: <c>Loc.GetString</c> or a <c>Popup*</c> call inside a per-tick query
/// loop. The heuristic is: the invocation sits inside a <c>while (query.MoveNext())</c>
/// loop whose enclosing method is an <c>override</c> named <c>Update</c> or
/// <c>FrameUpdate</c>. Such calls run every tick for every matched entity and
/// allocate (string formatting plus boxed tuple args) on each iteration, which is
/// steady GC pressure. Hoist the call out of the loop or guard it behind a
/// condition that is usually false.
/// Scoped to fork files (path contains <c>/RussStation/</c> or ends in
/// <c>.Honk.cs</c>) so upstream drift is not this rule's problem.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HonkEagerStringInTickLoopAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Descriptor = new(
        id: "HONK0027",
        title: "Eager string/popup inside a per-tick query loop",
        messageFormat: "{0} runs every tick for every matched entity inside an Update query loop; hoist it out or guard it",
        category: "Honk.Perf",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Loc.GetString and popup calls inside a while (query.MoveNext()) loop in an Update override allocate (string formatting plus boxed tuple args) per entity per tick, producing steady GC pressure. Hoist the call out of the loop or guard it behind a condition that is rarely true.");

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

        if (!IsLocOrPopupCall(invocation))
            return;

        if (!IsInsideMoveNextWhileLoop(invocation))
            return;

        if (!IsInUpdateOverride(invocation))
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            Descriptor, invocation.GetLocation(), invocation.Expression.ToString()));
    }

    private static bool IsForkFile(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        return path!.Replace('\\', '/').Contains("/RussStation/")
            || path.EndsWith(".Honk.cs", StringComparison.Ordinal);
    }

    private static bool IsLocOrPopupCall(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax member)
            return false;

        var name = member.Name.Identifier.ValueText;

        // Loc.GetString
        if (member.Expression is IdentifierNameSyntax root
            && root.Identifier.ValueText == "Loc"
            && name == "GetString")
        {
            return true;
        }

        // PopupEntity / PopupClient / PopupCursor / PopupCoordinates / PopupPredicted
        return name.StartsWith("Popup", StringComparison.Ordinal);
    }

    private static bool IsInsideMoveNextWhileLoop(SyntaxNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (current is WhileStatementSyntax whileStmt
                && whileStmt.Condition.ToString().Contains("MoveNext"))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsInUpdateOverride(SyntaxNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (current is MethodDeclarationSyntax method)
            {
                var name = method.Identifier.ValueText;
                if (name != "Update" && name != "FrameUpdate")
                    return false;

                return method.Modifiers.Any(SyntaxKind.OverrideKeyword);
            }
        }
        return false;
    }
}
