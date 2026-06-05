using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Content.Analyzers.Honk;

/// <summary>
/// HONK0031: <c>Timer.Spawn(...)</c> with no <see cref="System.Threading.CancellationToken"/>
/// argument. Fire-and-forget <see cref="Robust.Shared.Timing.Timer"/> callbacks that capture
/// an entity run after the delay regardless of intervening state changes, so a stale or
/// overlapping callback can fire after the entity/state has changed. Passing a
/// <c>CancellationToken</c> lets the system cancel them.
/// Scoped to fork files (path contains <c>/RussStation/</c> or ends in
/// <c>.Honk.cs</c>) so upstream drift is not this rule's problem.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HonkTimerSpawnWithoutTokenAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Descriptor = new(
        id: "HONK0031",
        title: "Timer.Spawn without a CancellationToken",
        messageFormat: "Timer.Spawn has no CancellationToken; a stale or overlapping callback can fire after the entity/state has changed",
        category: "Honk.Lifecycle",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Fire-and-forget Timer.Spawn callbacks capturing an entity run after the delay regardless of intervening state changes; passing a CancellationToken lets the system cancel them.");

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

        if (member.Name.Identifier.ValueText != "Spawn")
            return;

        if (!IsTimerReceiver(member.Expression))
            return;

        foreach (var arg in invocation.ArgumentList.Arguments)
        {
            if (IsCancellationToken(context, arg))
                return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Descriptor, invocation.GetLocation()));
    }

    private static bool IsTimerReceiver(ExpressionSyntax expr)
    {
        return expr switch
        {
            IdentifierNameSyntax id => id.Identifier.ValueText == "Timer",
            MemberAccessExpressionSyntax m => m.Name.Identifier.ValueText == "Timer",
            _ => false,
        };
    }

    private static bool IsCancellationToken(SyntaxNodeAnalysisContext context, ArgumentSyntax arg)
    {
        var type = context.SemanticModel.GetTypeInfo(arg.Expression).Type;
        if (type is not null)
            return type.Name == "CancellationToken";

        // Fall back to a textual hint when the model can't resolve the type.
        var text = arg.Expression.ToString();
        return text.Contains("Token") || text.Contains("token");
    }

    private static bool IsForkFile(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        return path!.Replace('\\', '/').Contains("/RussStation/")
            || path.EndsWith(".Honk.cs", StringComparison.Ordinal);
    }
}
