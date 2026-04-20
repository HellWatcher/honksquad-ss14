using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Content.Analyzers.Honk;

/// <summary>
/// HONK0008: a non-abstract, non-<c>[Virtual]</c> fork class deriving from
/// <c>Robust.Shared.GameObjects.Component</c> must carry a
/// <c>[RegisterComponent]</c> attribute. Without it, the component is not
/// registered with the factory: <c>AddComp</c>/<c>EnsureComp</c> throw at
/// runtime and prototypes silently fail to attach it. Scoped to fork files
/// (path contains <c>/RussStation/</c> or ends in <c>.Honk.cs</c>) so
/// upstream-only dead code is not this rule's problem.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HonkComponentRegistrationAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Descriptor = new(
        id: "HONK0008",
        title: "Component subclass missing [RegisterComponent]",
        messageFormat: "'{0}' derives from Component but is missing [RegisterComponent]; add the attribute, mark the class abstract, or mark it [Virtual]",
        category: "Honk.Component",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Concrete Component subclasses must be registered via [RegisterComponent]. Missing the attribute means the component cannot be added, ensured, or attached through prototypes at runtime. Abstract classes and upstream [Virtual] base classes are exempt.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(Analyze, SymbolKind.NamedType);
    }

    private static void Analyze(SymbolAnalysisContext context)
    {
        if (context.Symbol is not INamedTypeSymbol type)
            return;

        if (type.TypeKind != TypeKind.Class || type.IsAbstract)
            return;

        if (!InheritsComponent(type))
            return;

        if (HasRegisterComponentAttribute(type))
            return;

        if (HasVirtualAttribute(type))
            return;

        if (!HasForkDeclaration(type))
            return;

        foreach (var location in type.Locations)
        {
            if (location.IsInSource)
                context.ReportDiagnostic(Diagnostic.Create(Descriptor, location, type.Name));
        }
    }

    private static bool HasForkDeclaration(INamedTypeSymbol type)
    {
        foreach (var location in type.Locations)
        {
            if (location.IsInSource && IsForkFile(location.SourceTree?.FilePath))
                return true;
        }
        return false;
    }

    private static bool IsForkFile(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return false;
        var norm = path!.Replace('\\', '/');
        return norm.Contains("/RussStation/") || norm.EndsWith(".Honk.cs", StringComparison.Ordinal);
    }

    private static bool InheritsComponent(INamedTypeSymbol type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current.Name == "Component" &&
                current.ContainingNamespace?.ToDisplayString() == "Robust.Shared.GameObjects")
            {
                return true;
            }
        }
        return false;
    }

    private static bool HasRegisterComponentAttribute(INamedTypeSymbol type)
    {
        foreach (var attribute in type.GetAttributes())
        {
            var attrClass = attribute.AttributeClass;
            if (attrClass is null)
                continue;

            if (attrClass.Name is "RegisterComponentAttribute" or "RegisterComponent" &&
                attrClass.ContainingNamespace?.ToDisplayString() == "Robust.Shared.GameObjects")
            {
                return true;
            }
        }
        return false;
    }

    private static bool HasVirtualAttribute(INamedTypeSymbol type)
    {
        foreach (var attribute in type.GetAttributes())
        {
            var attrClass = attribute.AttributeClass;
            if (attrClass is null)
                continue;

            if (attrClass.Name is "VirtualAttribute" or "Virtual" &&
                attrClass.ContainingNamespace?.ToDisplayString() == "Robust.Shared.Analyzers")
            {
                return true;
            }
        }
        return false;
    }
}
