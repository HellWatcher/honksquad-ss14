using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Content.Analyzers.Honk;

/// <summary>
/// HONK0012: a <c>[NetworkedComponent]</c> class that declares one or more
/// <c>[DataField]</c> members but has no <c>[AutoGenerateComponentState]</c>
/// attribute and no manual <c>GetComponentState</c> / <c>HandleComponentState</c>
/// override silently fails to replicate its fields. The mutation reaches
/// only the server. Scoped to fork files (path contains <c>/RussStation/</c>
/// or ends in <c>.Honk.cs</c>) so upstream drift is not this rule's problem.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HonkNetworkedComponentStateAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Descriptor = new(
        id: "HONK0012",
        title: "Networked component declares DataFields but has no state mechanism",
        messageFormat: "Networked component '{0}' declares [DataField] members but has no [AutoGenerateComponentState] and no GetComponentState/HandleComponentState override; fields silently fail to replicate",
        category: "Honk.Networking",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A [NetworkedComponent] with [DataField]s needs either auto-state generation or a manual GetComponentState/HandleComponentState pair. Without one, the component announces it is networked but never actually replicates, which is silent at runtime.");

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
        var cls = (ClassDeclarationSyntax)context.Node;

        if (!IsForkFile(cls.SyntaxTree.FilePath))
            return;

        if (context.SemanticModel.GetDeclaredSymbol(cls, context.CancellationToken) is not INamedTypeSymbol symbol)
            return;

        if (!HasAttribute(symbol, "NetworkedComponentAttribute"))
            return;

        if (HasAttribute(symbol, "AutoGenerateComponentStateAttribute"))
            return;

        if (!HasDataField(symbol))
            return;

        if (HasStateOverride(symbol))
            return;

        context.ReportDiagnostic(Diagnostic.Create(Descriptor, cls.Identifier.GetLocation(), symbol.Name));
    }

    private static bool IsForkFile(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        return path!.Replace('\\', '/').Contains("/RussStation/")
            || path.EndsWith(".Honk.cs", StringComparison.Ordinal);
    }

    private static bool HasAttribute(ISymbol symbol, string name)
    {
        foreach (var attr in symbol.GetAttributes())
        {
            if (attr.AttributeClass?.Name == name)
                return true;
        }
        return false;
    }

    private static bool HasDataField(INamedTypeSymbol type)
    {
        foreach (var member in type.GetMembers())
        {
            if (member is IFieldSymbol or IPropertySymbol)
            {
                foreach (var attr in member.GetAttributes())
                {
                    var attrName = attr.AttributeClass?.Name;
                    if (attrName == "DataFieldAttribute" || attrName == "AutoNetworkedFieldAttribute")
                        return true;
                }
            }
        }
        return false;
    }

    private static bool HasStateOverride(INamedTypeSymbol type)
    {
        foreach (var member in type.GetMembers())
        {
            if (member is not IMethodSymbol method)
                continue;
            var name = method.Name;
            if (name is "GetComponentState" or "HandleComponentState")
                return true;
        }
        return false;
    }
}
