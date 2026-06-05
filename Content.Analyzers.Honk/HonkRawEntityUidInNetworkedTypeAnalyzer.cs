using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Content.Analyzers.Honk;

/// <summary>
/// HONK0025: A raw <c>EntityUid</c> stored in a network-serialized type (a
/// <c>[NetSerializable]</c> type, a BUI message/state, or a DoAfter event) is the
/// sender's local entity id. It does not survive the client/server boundary and
/// resolves to the wrong or an invalid entity on the receiving side; the portable
/// form is <c>NetEntity</c>. Component-state auto-networking is exempt because the
/// <c>[AutoNetworkedField]</c> source generator translates EntityUid correctly.
/// Scoped to fork files so upstream drift is not this rule's problem.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HonkRawEntityUidInNetworkedTypeAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Descriptor = new(
        id: "HONK0025",
        title: "Raw EntityUid in a network-serialized type",
        messageFormat: "Member '{0}' is EntityUid-typed in a network-serialized type; use NetEntity to avoid client/server desync",
        category: "Honk.Networking",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A serialized EntityUid is the sender's local id and resolves to the wrong/invalid entity across the network boundary; NetEntity is the portable form.");

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

        // Component-state auto-networking translates EntityUid correctly; flagging it is wrong.
        if (HasAttribute(symbol, "AutoNetworkedFieldAttribute"))
            return;

        if (!IsNetworkSerializedType(symbol.ContainingType))
            return;

        var type = symbol switch
        {
            IFieldSymbol f => f.Type,
            IPropertySymbol p => p.Type,
            _ => null,
        };
        if (type is null)
            return;

        if (!IsEntityUidTyped(type))
            return;

        foreach (var location in symbol.Locations)
        {
            if (location.IsInSource)
                context.ReportDiagnostic(Diagnostic.Create(Descriptor, location, symbol.Name));
        }
    }

    private static bool IsNetworkSerializedType(INamedTypeSymbol? type)
    {
        if (type is null)
            return false;

        // [NetSerializable] must be declared on the type ITSELF; it is not
        // inherited for serialization. Walking base types for the attribute
        // would wrongly flag every local event, because Robust's EntityEventArgs
        // base is itself [NetSerializable] — yet a raw EntityUid in a local
        // (RaiseLocalEvent) event is correct and must not be reported.
        foreach (var attr in type.GetAttributes())
        {
            if (attr.AttributeClass?.Name == "NetSerializableAttribute")
                return true;
        }

        // Otherwise it only counts if it derives a base that is inherently
        // networked (BUI messages/states and DoAfter events always cross the
        // wire). A bare EntityEventArgs does not qualify.
        for (var current = type; current is not null; current = current.BaseType)
        {
            switch (current.Name)
            {
                case "BoundUserInterfaceMessage":
                case "BoundUserInterfaceState":
                case "DoAfterEvent":
                case "SimpleDoAfterEvent":
                    return true;
            }
        }

        return false;
    }

    private static bool IsEntityUidTyped(ITypeSymbol type)
    {
        // Unwrap Nullable<T>.
        if (type is INamedTypeSymbol nullable && nullable.IsGenericType && nullable.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T)
            type = nullable.TypeArguments[0];

        if (type.Name == "EntityUid")
            return true;

        // EntityUid[].
        if (type is IArrayTypeSymbol array && array.ElementType.Name == "EntityUid")
            return true;

        // List<EntityUid>, HashSet<EntityUid>, etc.
        if (type is INamedTypeSymbol named && named.IsGenericType)
        {
            foreach (var arg in named.TypeArguments)
            {
                if (arg.Name == "EntityUid")
                    return true;
            }
        }

        return false;
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
