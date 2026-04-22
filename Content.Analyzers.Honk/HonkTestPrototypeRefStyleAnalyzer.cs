using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Content.Analyzers.Honk;

/// <summary>
/// HONK0014: inside <c>Content.IntegrationTests</c>, a <c>ProtoId&lt;T&gt;</c>
/// field or property must not use a string literal whose value is declared
/// as a prototype id inside a <c>[TestPrototypes]</c> YAML block. The YAML
/// linter never sees <c>[TestPrototypes]</c> prototypes, so the compilation
/// passes but CI fails with <c>Unknown prototype</c>. Use
/// <c>private const string Foo = "Foo";</c> + <c>protoMan.Index&lt;T&gt;(Foo)</c> instead.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HonkTestPrototypeRefStyleAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Descriptor = new(
        id: "HONK0014",
        title: "ProtoId<T> field refers to [TestPrototypes]-declared id",
        messageFormat: "'{0}' is declared in a [TestPrototypes] YAML block; store it as 'private const string' and resolve through protoMan.Index<T>() instead of ProtoId<T>",
        category: "Honk.TestPrototypes",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The YAML linter does not load [TestPrototypes] YAML blocks, so ProtoId<T> fields pointing at those ids fail CI with 'Unknown prototype'. Use a private const string threaded through IPrototypeManager.Index<T>() to dodge both the linter and upstream's [ForbidLiteral] RA0033.");

    private static readonly Regex IdRegex = new(@"id:\s*(?:\{(?<name>\w+)\}|(?<lit>\w+))", RegexOptions.Compiled);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var assembly = context.Compilation.AssemblyName ?? string.Empty;
        if (!assembly.StartsWith("Content.IntegrationTests", StringComparison.Ordinal))
            return;

        // TestIds are a cross-tree lookup, but we compute them lazily the first
        // time a tree's field/property analysis actually needs them. Each tree
        // still reports locally, so code fixes can engage.
        var testIds = new Lazy<HashSet<string>>(
            () => ComputeTestIds(context.Compilation, context.CancellationToken),
            LazyThreadSafetyMode.ExecutionAndPublication);

        context.RegisterSyntaxNodeAction(ctx => AnalyzeField(ctx, testIds), SyntaxKind.FieldDeclaration);
        context.RegisterSyntaxNodeAction(ctx => AnalyzeProperty(ctx, testIds), SyntaxKind.PropertyDeclaration);
    }

    private static void AnalyzeField(SyntaxNodeAnalysisContext context, Lazy<HashSet<string>> testIds)
    {
        var field = (FieldDeclarationSyntax)context.Node;
        if (!IsProtoIdType(field.Declaration.Type, context.SemanticModel, context.CancellationToken))
            return;

        foreach (var declarator in field.Declaration.Variables)
        {
            if (declarator.Initializer?.Value is not LiteralExpressionSyntax lit
                || !lit.IsKind(SyntaxKind.StringLiteralExpression))
                continue;

            ReportIfMatch(context, lit, testIds);
        }
    }

    private static void AnalyzeProperty(SyntaxNodeAnalysisContext context, Lazy<HashSet<string>> testIds)
    {
        var prop = (PropertyDeclarationSyntax)context.Node;
        if (!IsProtoIdType(prop.Type, context.SemanticModel, context.CancellationToken))
            return;

        if (prop.Initializer?.Value is not LiteralExpressionSyntax lit
            || !lit.IsKind(SyntaxKind.StringLiteralExpression))
            return;

        ReportIfMatch(context, lit, testIds);
    }

    private static void ReportIfMatch(
        SyntaxNodeAnalysisContext context,
        LiteralExpressionSyntax literal,
        Lazy<HashSet<string>> testIds)
    {
        var location = literal.GetLocation();
        if (!IsForkFile(location.SourceTree?.FilePath))
            return;

        var value = literal.Token.ValueText;
        if (!testIds.Value.Contains(value))
            return;

        context.ReportDiagnostic(Diagnostic.Create(Descriptor, location, value));
    }

    private static HashSet<string> ComputeTestIds(Compilation compilation, CancellationToken ct)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var constValues = new Dictionary<string, string>(StringComparer.Ordinal);
        var rawYaml = new List<string>();

        foreach (var tree in compilation.SyntaxTrees)
        {
            ct.ThrowIfCancellationRequested();
            var root = tree.GetRoot(ct);

            foreach (var field in root.DescendantNodes().OfType<FieldDeclarationSyntax>())
            {
                if (IsConstString(field))
                {
                    foreach (var declarator in field.Declaration.Variables)
                    {
                        if (declarator.Initializer?.Value is LiteralExpressionSyntax lit
                            && lit.IsKind(SyntaxKind.StringLiteralExpression))
                        {
                            constValues[declarator.Identifier.ValueText] = lit.Token.ValueText;
                        }
                    }
                }

                if (!HasTestPrototypesAttributeSyntax(field))
                    continue;

                foreach (var declarator in field.Declaration.Variables)
                {
                    var raw = ExtractRawText(declarator.Initializer?.Value);
                    if (raw is not null)
                        rawYaml.Add(raw);
                }
            }
        }

        foreach (var raw in rawYaml)
        {
            foreach (Match match in IdRegex.Matches(raw))
            {
                var name = match.Groups["name"].Value;
                if (!string.IsNullOrEmpty(name))
                {
                    if (constValues.TryGetValue(name, out var value))
                        ids.Add(value);
                }
                else
                {
                    ids.Add(match.Groups["lit"].Value);
                }
            }
        }

        return ids;
    }

    private static bool IsForkFile(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return false;
        var norm = path!.Replace('\\', '/');
        return norm.Contains("/RussStation/") || norm.EndsWith(".Honk.cs", StringComparison.Ordinal);
    }

    private static bool IsConstString(FieldDeclarationSyntax field)
    {
        var isConst = false;
        foreach (var modifier in field.Modifiers)
        {
            if (modifier.IsKind(SyntaxKind.ConstKeyword))
                isConst = true;
        }
        if (!isConst)
            return false;

        return field.Declaration.Type is PredefinedTypeSyntax pts && pts.Keyword.IsKind(SyntaxKind.StringKeyword);
    }

    private static bool HasTestPrototypesAttributeSyntax(FieldDeclarationSyntax field)
    {
        foreach (var list in field.AttributeLists)
        {
            foreach (var attr in list.Attributes)
            {
                var name = attr.Name switch
                {
                    QualifiedNameSyntax q => q.Right.Identifier.ValueText,
                    SimpleNameSyntax s => s.Identifier.ValueText,
                    _ => attr.Name.ToString(),
                };
                if (name == "TestPrototypes" || name == "TestPrototypesAttribute")
                    return true;
            }
        }
        return false;
    }

    private static string? ExtractRawText(ExpressionSyntax? expr)
    {
        return expr switch
        {
            LiteralExpressionSyntax lit when lit.IsKind(SyntaxKind.StringLiteralExpression) => lit.Token.ValueText,
            InterpolatedStringExpressionSyntax interp => interp.ToString(),
            _ => expr?.ToString(),
        };
    }

    private static bool IsProtoIdType(TypeSyntax? typeSyntax, SemanticModel model, CancellationToken ct)
    {
        if (typeSyntax is null)
            return false;
        var type = model.GetTypeInfo(typeSyntax, ct).Type;
        return type?.Name == "ProtoId";
    }
}
