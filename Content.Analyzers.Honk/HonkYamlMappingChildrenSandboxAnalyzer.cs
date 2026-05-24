using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Content.Analyzers.Honk;

/// <summary>
/// HONK0021: <c>YamlMappingNode.Children</c> resolves to
/// <c>IDictionary&lt;YamlNode, YamlNode&gt;</c>, which fails the engine
/// sandbox typecheck on Shared and Client. Code compiles, then the
/// assembly fails to load. The mechanical fix is to iterate the node
/// directly with <c>foreach</c>, which goes through the allowed
/// enumerator. Scoped to fork files in sandboxed assemblies.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HonkYamlMappingChildrenSandboxAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Descriptor = new(
        id: "HONK0021",
        title: "YamlMappingNode.Children access in sandboxed code",
        messageFormat: "YamlMappingNode.Children is sandbox-banned in Shared/Client; iterate the node directly with foreach instead",
        category: "Honk.Sandbox",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "YamlMappingNode.Children returns IDictionary<YamlNode, YamlNode>, which is not on the sandbox allowlist. The assembly loads fine on the developer's box and is rejected by the engine.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.SimpleMemberAccessExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var member = (MemberAccessExpressionSyntax)context.Node;

        if (member.Name.Identifier.ValueText != "Children")
            return;

        var path = member.SyntaxTree.FilePath;
        if (!IsForkFile(path) || !IsSandboxedAssemblyPath(path))
            return;

        var receiverType = context.SemanticModel.GetTypeInfo(member.Expression, context.CancellationToken).Type;
        if (receiverType is null || !IsYamlMappingNode(receiverType))
            return;

        context.ReportDiagnostic(Diagnostic.Create(Descriptor, member.Name.GetLocation()));
    }

    private static bool IsYamlMappingNode(ITypeSymbol type)
    {
        if (type.Name != "YamlMappingNode")
            return false;

        var ns = type.ContainingNamespace;
        return ns is { Name: "RepresentationModel" }
            && ns.ContainingNamespace is { Name: "YamlDotNet" };
    }

    private static bool IsForkFile(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        var normalized = path!.Replace('\\', '/');
        return normalized.Contains("/RussStation/")
            || normalized.EndsWith(".Honk.cs", StringComparison.Ordinal);
    }

    private static bool IsSandboxedAssemblyPath(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        var normalized = path!.Replace('\\', '/');
        return normalized.Contains("/Content.Shared/")
            || normalized.Contains("/Content.Client/")
            || normalized.StartsWith("Content.Shared/", StringComparison.Ordinal)
            || normalized.StartsWith("Content.Client/", StringComparison.Ordinal);
    }
}
