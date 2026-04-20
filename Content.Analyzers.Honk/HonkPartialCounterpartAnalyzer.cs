using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Content.Analyzers.Honk;

/// <summary>
/// HONK0006: every class declared in a `*.Honk.cs` file must be `partial`
/// and must have at least one declaring file that isn't itself a
/// `*.Honk.cs` file. This keeps Honk partials anchored to a real upstream
/// (or fork-owned) definition; a Honk partial with no counterpart is
/// really just a fork file with a misleading name and should live as a
/// normal `.cs` file instead.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HonkPartialCounterpartAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor MissingPartial = new(
        id: "HONK0006",
        title: "Honk partial missing partial modifier or counterpart",
        messageFormat: "'{0}' in a .Honk.cs file {1}; rename the file to plain .cs or declare the counterpart",
        category: "Honk.Access",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A `.Honk.cs` file is a fork augmentation of an existing type. Classes declared there must be `partial` and must have at least one non-Honk declaration, otherwise the file is a fork addition hiding under the Honk naming convention.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(MissingPartial);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ClassDeclaration);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var cls = (ClassDeclarationSyntax)context.Node;

        if (!IsHonkPartialFile(cls.SyntaxTree.FilePath))
            return;

        if (!cls.Modifiers.Any(SyntaxKind.PartialKeyword))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                MissingPartial,
                cls.Identifier.GetLocation(),
                cls.Identifier.ValueText,
                "is not declared partial"));
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(cls, context.CancellationToken) is not INamedTypeSymbol symbol)
            return;

        foreach (var reference in symbol.DeclaringSyntaxReferences)
        {
            if (!IsHonkPartialFile(reference.SyntaxTree.FilePath))
                return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            MissingPartial,
            cls.Identifier.GetLocation(),
            cls.Identifier.ValueText,
            "has no non-Honk counterpart declaration"));
    }

    private static bool IsHonkPartialFile(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return false;
        return path!.Replace('\\', '/').EndsWith(".Honk.cs", StringComparison.Ordinal);
    }
}
