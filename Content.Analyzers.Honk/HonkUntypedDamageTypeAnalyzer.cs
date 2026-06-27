using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Content.Analyzers.Honk;

/// <summary>
/// HONK0024: a raw string literal that names a standard SS14 damage type
/// (e.g. <c>"Slash"</c>, <c>"Blunt"</c>) used in a damage context -- an
/// equality comparison, a dictionary indexer key, a collection/object
/// initializer element, or an invocation argument. Damage types should be
/// referenced through <c>ProtoId&lt;DamageTypePrototype&gt;</c> so a renamed
/// or removed prototype is a compile error. As a raw string the same rename
/// silently turns into a dictionary miss with no diagnostic at build time.
/// Scoped to fork files (path contains <c>/RussStation/</c> or ends in
/// <c>.Honk.cs</c>) so upstream drift is not this rule's problem.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HonkUntypedDamageTypeAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Descriptor = new(
        id: "HONK0024",
        title: "Untyped damage-type string literal",
        messageFormat: "Damage-type string literal \"{0}\"; use ProtoId<DamageTypePrototype> instead of a raw string",
        category: "Honk.Drift",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A renamed or removed damage prototype referenced by a raw string fails silently as a dictionary miss with no compile error. ProtoId<DamageTypePrototype> turns the same rename into a build break.");

    private static readonly HashSet<string> DamageTypes = new(StringComparer.Ordinal)
    {
        "Blunt",
        "Slash",
        "Piercing",
        "Heat",
        "Shock",
        "Cold",
        "Caustic",
        "Poison",
        "Radiation",
        "Asphyxiation",
        "Bloodloss",
        "Cellular",
        "Structural",
    };

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.StringLiteralExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var literal = (LiteralExpressionSyntax)context.Node;

        if (!IsForkFile(literal.SyntaxTree.FilePath))
            return;

        var value = literal.Token.ValueText;
        if (!DamageTypes.Contains(value))
            return;

        if (!IsDamageContext(literal))
            return;

        context.ReportDiagnostic(Diagnostic.Create(Descriptor, literal.GetLocation(), value));
    }

    private static bool IsDamageContext(ExpressionSyntax literal)
    {
        var parent = literal.Parent;

        switch (parent)
        {
            // (a) operand of an == or != comparison.
            case BinaryExpressionSyntax binary
                when binary.IsKind(SyntaxKind.EqualsExpression) || binary.IsKind(SyntaxKind.NotEqualsExpression):
                return true;

            // (b) the key of a dictionary indexer: dict["Blunt"].
            case ArgumentSyntax { Parent: BracketedArgumentListSyntax { Parent: ElementAccessExpressionSyntax } }:
                return true;

            // (c) an element inside a collection / object initializer.
            case InitializerExpressionSyntax:
                return true;

            // (d) an argument in an invocation's argument list.
            case ArgumentSyntax { Parent: ArgumentListSyntax { Parent: InvocationExpressionSyntax } }:
                return true;
        }

        return false;
    }

    private static bool IsForkFile(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        var normalized = path!.Replace('\\', '/');
        return normalized.Contains("/RussStation/")
            || normalized.EndsWith(".Honk.cs", StringComparison.Ordinal);
    }
}
