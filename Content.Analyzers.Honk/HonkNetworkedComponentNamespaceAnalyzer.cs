using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Content.Analyzers.Honk;

/// <summary>
/// HONK0023: <c>NetworkedComponentAttribute</c> lives in
/// <c>Robust.Shared.GameStates</c>. The closely named
/// <c>Robust.Shared.GameObjects</c> is a frequent typo that compiles when
/// the attribute happens to resolve via another <c>using</c>, and the
/// wrong import tends to travel with other namespace mistakes. Flagged at
/// the attribute site as a confusion signal, not a hard error.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HonkNetworkedComponentNamespaceAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Descriptor = new(
        id: "HONK0023",
        title: "[NetworkedComponent] without using Robust.Shared.GameStates",
        messageFormat: "Class '{0}' is decorated with [NetworkedComponent] but the file does not import Robust.Shared.GameStates; add 'using Robust.Shared.GameStates;'",
        category: "Honk.Networking",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "NetworkedComponentAttribute lives in Robust.Shared.GameStates. Missing that using often travels with other Robust.Shared.GameObjects-vs-GameStates namespace mistakes.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ClassDeclaration);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;

        var path = classDecl.SyntaxTree.FilePath;
        if (!IsForkFile(path))
            return;

        var attribute = FindNetworkedComponentAttribute(classDecl, context);
        if (attribute is null)
            return;

        if (HasGameStatesUsing(classDecl.SyntaxTree))
            return;

        context.ReportDiagnostic(Diagnostic.Create(Descriptor, attribute.GetLocation(), classDecl.Identifier.ValueText));
    }

    private static AttributeSyntax? FindNetworkedComponentAttribute(ClassDeclarationSyntax classDecl, SyntaxNodeAnalysisContext context)
    {
        foreach (var list in classDecl.AttributeLists)
        {
            foreach (var attr in list.Attributes)
            {
                var symbol = context.SemanticModel.GetSymbolInfo(attr, context.CancellationToken).Symbol;
                var attrType = (symbol as IMethodSymbol)?.ContainingType;
                if (attrType?.Name == "NetworkedComponentAttribute")
                    return attr;
            }
        }
        return null;
    }

    private static bool HasGameStatesUsing(SyntaxTree tree)
    {
        var root = tree.GetCompilationUnitRoot();
        return UsingsContain(root.Usings, "Robust.Shared.GameStates")
            || root.Members.OfType<BaseNamespaceDeclarationSyntax>().Any(ns => UsingsContain(ns.Usings, "Robust.Shared.GameStates"));
    }

    private static bool UsingsContain(SyntaxList<UsingDirectiveSyntax> usings, string namespaceName)
    {
        foreach (var u in usings)
        {
            if (u.Name?.ToString() == namespaceName)
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
