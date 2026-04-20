using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Content.Analyzers.Honk;

/// <summary>
/// HONK0004: every <c>// HONK START</c> marker must be closed by a
/// <c>// HONK END</c> marker in the same file, and vice-versa.
/// Conflict resolutions during rebase regularly drop one half of the
/// pair, which leaves a silent unmarked region or an orphan END.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HonkMarkerBalanceAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Descriptor = new(
        id: "HONK0004",
        title: "HONK marker is unbalanced",
        messageFormat: "{0} marker has no matching {1} marker in this file",
        category: "Honk.Markers",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "`// HONK START` and `// HONK END` must appear as balanced pairs. An orphan marker means a previous rebase dropped one side of a fork-owned region.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxTreeAction(Analyze);
    }

    private static void Analyze(SyntaxTreeAnalysisContext context)
    {
        var text = context.Tree.GetText(context.CancellationToken);
        TextSpan? openMarker = null;

        foreach (var line in text.Lines)
        {
            var payload = CommentPayload(text.ToString(line.Span));
            if (payload is null)
                continue;

            if (payload.StartsWith("HONK START", StringComparison.Ordinal))
            {
                if (openMarker is { } prev)
                {
                    // Report the previous unmatched START, then continue tracking the new one.
                    context.ReportDiagnostic(Diagnostic.Create(
                        Descriptor,
                        Location.Create(context.Tree, prev),
                        "HONK START", "HONK END"));
                }
                openMarker = line.Span;
            }
            else if (payload.StartsWith("HONK END", StringComparison.Ordinal))
            {
                if (openMarker is null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        Descriptor,
                        Location.Create(context.Tree, line.Span),
                        "HONK END", "HONK START"));
                }
                else
                {
                    openMarker = null;
                }
            }
        }

        if (openMarker is { } trailing)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Descriptor,
                Location.Create(context.Tree, trailing),
                "HONK START", "HONK END"));
        }
    }

    private static string? CommentPayload(string line)
    {
        var trimmed = line.TrimStart();
        if (!trimmed.StartsWith("//", StringComparison.Ordinal))
            return null;
        return trimmed.Substring(2).TrimStart();
    }
}
