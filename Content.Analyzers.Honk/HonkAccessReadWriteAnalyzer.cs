using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Content.Analyzers.Honk;

/// <summary>
/// HONK0001: forbids `[Access(..., Other = AccessPermissions.ReadWrite)]`
/// inside a `// HONK` block. The `Other = ReadWrite` escape hatch re-exports
/// write access to every assembly. Fork policy prefers a partial class with
/// a named setter, or a fork-friend `[Access(typeof(ForkSystem))]` addition.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HonkAccessReadWriteAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Descriptor = new(
        id: "HONK0001",
        title: "Access.Other = ReadWrite inside HONK block",
        messageFormat: "[Access] with Other = ReadWrite is forbidden inside a HONK block; use a partial class setter or fork-friend [Access] instead",
        category: "Honk.Access",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The fork policy reserves `Other = AccessPermissions.ReadWrite` for cases where upstream co-owns the component. Widening write access from fork code hides the friendship; use a partial class or a HONK-marked fork-friend [Access] instead.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.Attribute);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var attribute = (AttributeSyntax)context.Node;

        if (!IsAccessAttributeName(attribute.Name))
            return;

        var readWriteArg = FindReadWriteOther(attribute);
        if (readWriteArg is null)
            return;

        var blocks = HonkMarkerBlocks.Find(context.Node.SyntaxTree.GetText(context.CancellationToken));
        if (!HonkMarkerBlocks.Contains(blocks, attribute.Span))
            return;

        context.ReportDiagnostic(Diagnostic.Create(Descriptor, attribute.GetLocation()));
    }

    private static bool IsAccessAttributeName(NameSyntax name)
    {
        var simple = name switch
        {
            SimpleNameSyntax s => s.Identifier.ValueText,
            QualifiedNameSyntax q => q.Right.Identifier.ValueText,
            _ => null,
        };

        return simple is "Access" or "AccessAttribute";
    }

    private static AttributeArgumentSyntax? FindReadWriteOther(AttributeSyntax attribute)
    {
        var args = attribute.ArgumentList?.Arguments;
        if (args is null)
            return null;

        foreach (var arg in args)
        {
            if (arg.NameEquals?.Name.Identifier.ValueText != "Other")
                continue;

            if (ReferencesReadWrite(arg.Expression))
                return arg;
        }

        return null;
    }

    private static bool ReferencesReadWrite(ExpressionSyntax expression)
    {
        return expression switch
        {
            MemberAccessExpressionSyntax m => m.Name.Identifier.ValueText == "ReadWrite",
            IdentifierNameSyntax i => i.Identifier.ValueText == "ReadWrite",
            _ => false,
        };
    }
}
