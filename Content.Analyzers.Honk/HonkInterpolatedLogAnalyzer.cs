using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Content.Analyzers.Honk;

/// <summary>
/// HONK0028: a logger call such as <c>Log.Warning($"...")</c> /
/// <c>_sawmill.Debug("a" + b)</c> whose first argument is an interpolated
/// or concatenated string. ISawmill/Log levels are filtered at call time,
/// so building the message eagerly allocates even when the level is
/// disabled. The receiver is matched by name (Log/Logger/*sawmill*)
/// because Robust is not referenced in the analyzer tests.
/// Scoped to fork files (path contains <c>/RussStation/</c> or ends in
/// <c>.Honk.cs</c>) so upstream drift is not this rule's problem.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HonkInterpolatedLogAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Descriptor = new(
        id: "HONK0028",
        title: "Interpolated string in a log call",
        messageFormat: "Log.{0} builds an interpolated/concatenated string before the level check; it allocates even when the level is disabled",
        category: "Honk.Logging",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "ISawmill/Log levels are filtered at call time; building the message string eagerly wastes allocations on disabled levels.");

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

        if (invocation.Expression is not MemberAccessExpressionSyntax member)
            return;

        var methodName = member.Name.Identifier.ValueText;
        if (methodName is not ("Debug" or "Info" or "Warning" or "Error" or "Fatal" or "Verbose"))
            return;

        if (!IsLoggerReceiver(member.Expression))
            return;

        if (invocation.ArgumentList.Arguments.Count < 1)
            return;

        var firstArg = invocation.ArgumentList.Arguments[0].Expression;
        if (!IsEagerString(firstArg))
            return;

        context.ReportDiagnostic(Diagnostic.Create(Descriptor, invocation.GetLocation(), methodName));
    }

    private static bool IsForkFile(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        return path!.Replace('\\', '/').Contains("/RussStation/")
            || path.EndsWith(".Honk.cs", StringComparison.Ordinal);
    }

    private static bool IsLoggerReceiver(ExpressionSyntax receiver)
    {
        var name = receiver switch
        {
            IdentifierNameSyntax id => id.Identifier.ValueText,
            MemberAccessExpressionSyntax m => m.Name.Identifier.ValueText,
            _ => null,
        };

        if (name is null)
            return false;

        if (name == "Log" || name == "Logger")
            return true;

        return name.IndexOf("sawmill", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsEagerString(ExpressionSyntax expr)
    {
        return expr is InterpolatedStringExpressionSyntax
            || (expr is BinaryExpressionSyntax binary && binary.IsKind(SyntaxKind.AddExpression));
    }
}
