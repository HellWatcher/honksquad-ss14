using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Content.Analyzers.Honk;

/// <summary>
/// HONK0029: <c>Loc.GetString($"{hole}...")</c> where the fluent key begins
/// with an interpolation hole. A key whose leading token is dynamic cannot be
/// discovered by string-extraction / linter tooling, so it can ship
/// missing or typo'd to players with no static warning. Keys with a literal
/// prefix (e.g. <c>$"wound-{tier}"</c>) stay extractable and are allowed.
/// Scoped to fork files (path contains <c>/RussStation/</c> or ends in
/// <c>.Honk.cs</c>) so upstream drift is not this rule's problem.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HonkOpaqueLocKeyAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Descriptor = new(
        id: "HONK0029",
        title: "Opaque dynamic Loc.GetString key",
        messageFormat: "Loc.GetString key begins with an interpolation hole; the key cannot be statically extracted or linted and may ship missing/typo'd to players",
        category: "Honk.Localization",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A fluent key whose leading token is dynamic cannot be discovered by string-extraction/linter tooling; keys with a literal prefix (e.g. $\"wound-{tier}\") remain extractable and are fine.");

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

        if (!IsLocGetString(invocation))
            return;

        if (invocation.ArgumentList.Arguments.Count < 1)
            return;

        var firstArg = invocation.ArgumentList.Arguments[0].Expression;
        if (firstArg is not InterpolatedStringExpressionSyntax interpolated)
            return;

        if (interpolated.Contents.Count == 0)
            return;

        if (interpolated.Contents[0] is not InterpolationSyntax)
            return;

        context.ReportDiagnostic(Diagnostic.Create(Descriptor, firstArg.GetLocation()));
    }

    private static bool IsForkFile(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        return path!.Replace('\\', '/').Contains("/RussStation/")
            || path.EndsWith(".Honk.cs", StringComparison.Ordinal);
    }

    private static bool IsLocGetString(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax member)
            return false;

        if (member.Expression is not IdentifierNameSyntax root || root.Identifier.ValueText != "Loc")
            return false;

        return member.Name.Identifier.ValueText == "GetString";
    }
}
