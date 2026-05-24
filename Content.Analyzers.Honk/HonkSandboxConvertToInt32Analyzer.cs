using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Content.Analyzers.Honk;

/// <summary>
/// HONK0020: the engine sandbox bans <c>System.Convert.ToInt32(object)</c>.
/// Code compiles on a developer machine, then fails the sandbox typecheck
/// on CI or at runtime. The common trigger is the generic enum-to-int
/// idiom; the replacement is the one-liner <c>(int)(object)value</c>.
/// Scoped to fork files under <c>Content.Shared/</c> or
/// <c>Content.Client/</c> (the two sandboxed assemblies).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HonkSandboxConvertToInt32Analyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Descriptor = new(
        id: "HONK0020",
        title: "Convert.ToInt32(object) in sandboxed code",
        messageFormat: "System.Convert.ToInt32(object) is sandbox-banned in Shared/Client; use (int)(object)value for the enum-to-int case",
        category: "Honk.Sandbox",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Convert.ToInt32(object) is not on the sandbox allowlist. The build succeeds locally but the assembly is rejected on load.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        var path = invocation.SyntaxTree.FilePath;
        if (!IsForkFile(path) || !IsSandboxedAssemblyPath(path))
            return;

        var symbol = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol as IMethodSymbol;
        if (symbol is null)
            return;

        if (symbol.Name != "ToInt32")
            return;

        var containing = symbol.ContainingType;
        if (containing is null || containing.Name != "Convert" || containing.ContainingNamespace is not { Name: "System" })
            return;

        if (symbol.Parameters.Length != 1 || symbol.Parameters[0].Type.SpecialType != SpecialType.System_Object)
            return;

        context.ReportDiagnostic(Diagnostic.Create(Descriptor, invocation.GetLocation()));
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
