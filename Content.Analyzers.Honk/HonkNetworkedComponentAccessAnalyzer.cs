using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Content.Analyzers.Honk;

/// <summary>
/// HONK0018: a fork class deriving from <c>Robust.Shared.GameObjects.Component</c> that
/// participates in network state replication should carry an <c>[Access(typeof(...))]</c>
/// attribute so external writers fail at build time. "Networked" here means the class is
/// itself <c>[NetworkedComponent]</c>, opts into <c>[AutoGenerateComponentState]</c>, or
/// holds a member tagged <c>[AutoNetworkedField]</c>; non-networked tag/marker components
/// are out of scope (#639). Severity is <c>Info</c>: plenty of legitimate fork components
/// don't need <c>[Access]</c>, so this surfaces in the IDE without breaking builds.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HonkNetworkedComponentAccessAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Descriptor = new(
        id: "HONK0018",
        title: "Networked fork component missing [Access]",
        messageFormat: "Networked fork component '{0}' has no [Access] attribute. Consider [Access(typeof(<OwningSystem>))] so missed Dirty() calls and external writes fail at build time.",
        category: "Honk.Component",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Fork components that replicate state via [NetworkedComponent], [AutoGenerateComponentState], or [AutoNetworkedField] should declare an owning system via [Access]. Without it, any code path can write fields that need a Dirty() call, and invariants tied to the owning system can drift silently. Tag/marker components are out of scope.");

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

        if (type.TypeKind != TypeKind.Class)
            return;

        if (!HasForkDeclaration(type))
            return;

        if (!InheritsComponent(type))
            return;

        if (HasAccessAttribute(type))
            return;

        if (!IsNetworked(type))
            return;

        foreach (var location in type.Locations)
        {
            if (location.IsInSource && IsForkFile(location.SourceTree?.FilePath))
            {
                context.ReportDiagnostic(Diagnostic.Create(Descriptor, location, type.Name));
                return;
            }
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

    private static bool HasAccessAttribute(INamedTypeSymbol type)
    {
        foreach (var attribute in type.GetAttributes())
        {
            var attrClass = attribute.AttributeClass;
            if (attrClass is null)
                continue;
            if (attrClass.Name is "AccessAttribute" or "Access")
                return true;
        }
        return false;
    }

    private static bool IsNetworked(INamedTypeSymbol type)
    {
        if (HasAttribute(type, "NetworkedComponentAttribute", "Robust.Shared.GameObjects")
            || HasAttribute(type, "NetworkedComponentAttribute", "Robust.Shared.GameStates")
            || HasAttribute(type, "AutoGenerateComponentStateAttribute", null))
        {
            return true;
        }

        foreach (var member in type.GetMembers())
        {
            if (member is not IFieldSymbol && member is not IPropertySymbol)
                continue;

            foreach (var attribute in member.GetAttributes())
            {
                var name = attribute.AttributeClass?.Name;
                if (name is "AutoNetworkedFieldAttribute")
                    return true;
            }
        }

        return false;
    }

    private static bool HasAttribute(INamedTypeSymbol type, string name, string? requiredNamespace)
    {
        foreach (var attribute in type.GetAttributes())
        {
            var attrClass = attribute.AttributeClass;
            if (attrClass is null)
                continue;
            if (attrClass.Name != name && attrClass.Name != name.Replace("Attribute", string.Empty))
                continue;
            if (requiredNamespace is null)
                return true;
            if (attrClass.ContainingNamespace?.ToDisplayString() == requiredNamespace)
                return true;
        }
        return false;
    }
}
