using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Content.Analyzers.Honk;

/// <summary>
/// HONK0019: <c>DoAfterId</c> is not <c>NetSerializable</c>, so wrapping a
/// field of that type in <c>[AutoNetworkedField]</c> compiles fine but
/// crashes the source-generated component state serializer at runtime
/// (and trips the YAML linter on the affected prototype). Scoped to fork
/// files so upstream drift is not this rule's problem.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HonkAutoNetworkedDoAfterIdAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Descriptor = new(
        id: "HONK0019",
        title: "[AutoNetworkedField] on DoAfterId-typed member",
        messageFormat: "Member '{0}' is DoAfterId-typed and cannot be auto-networked; drop [AutoNetworkedField] and call Dirty() manually",
        category: "Honk.Networking",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "DoAfterId is not NetSerializable. The auto-state source generator emits a serializer that throws at runtime, so the prototype fails to load.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.Field, SymbolKind.Property);
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context)
    {
        var symbol = context.Symbol;

        var path = symbol.Locations.Length > 0 ? symbol.Locations[0].SourceTree?.FilePath : null;
        if (!IsForkFile(path))
            return;

        if (!HasAttribute(symbol, "AutoNetworkedFieldAttribute"))
            return;

        var type = symbol switch
        {
            IFieldSymbol f => f.Type,
            IPropertySymbol p => p.Type,
            _ => null,
        };
        if (type is null)
            return;

        if (!IsDoAfterId(type))
            return;

        foreach (var location in symbol.Locations)
        {
            if (location.IsInSource)
                context.ReportDiagnostic(Diagnostic.Create(Descriptor, location, symbol.Name));
        }
    }

    private static bool IsDoAfterId(ITypeSymbol type)
    {
        // Unwrap Nullable<T>.
        if (type is INamedTypeSymbol named && named.IsGenericType && named.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T)
            type = named.TypeArguments[0];

        if (type.Name != "DoAfterId")
            return false;

        var ns = type.ContainingNamespace;
        return ns is { Name: "DoAfter" }
            && ns.ContainingNamespace is { Name: "Shared" }
            && ns.ContainingNamespace.ContainingNamespace is { Name: "Content" };
    }

    private static bool HasAttribute(ISymbol symbol, string attributeName)
    {
        foreach (var attr in symbol.GetAttributes())
        {
            if (attr.AttributeClass?.Name == attributeName)
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
