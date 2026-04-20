using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
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
        description: "The YAML linter does not load [TestPrototypes] YAML blocks, so ProtoId<T> fields pointing at those ids fail CI with 'Unknown prototype'. Use a private const string threaded through IPrototypeManager.Index<T>() to dodge both the linter and upstream's [ForbidLiteral] RA0033.",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    private static readonly Regex IdRegex = new(@"id:\s*(?:\{(?<name>\w+)\}|(?<lit>\w+))", RegexOptions.Compiled);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private sealed class State
    {
        public readonly ConcurrentDictionary<string, string> ConstValues = new();
        public readonly ConcurrentBag<string> TestPrototypeYaml = new();
        public readonly ConcurrentBag<(string Value, Location Location)> ProtoIdLiterals = new();
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var assembly = context.Compilation.AssemblyName ?? string.Empty;
        if (!assembly.StartsWith("Content.IntegrationTests", System.StringComparison.Ordinal))
            return;

        var state = new State();

        context.RegisterSemanticModelAction(ctx => Collect(ctx, state));
        context.RegisterCompilationEndAction(ctx => Report(ctx, state));
    }

    private static void Collect(SemanticModelAnalysisContext context, State state)
    {
        var root = context.SemanticModel.SyntaxTree.GetRoot(context.CancellationToken);
        var model = context.SemanticModel;

        foreach (var field in root.DescendantNodes().OfType<FieldDeclarationSyntax>())
        {
            var hasTestProto = HasTestPrototypesAttribute(field, model, context.CancellationToken);

            if (IsConstString(field))
            {
                foreach (var declarator in field.Declaration.Variables)
                {
                    if (declarator.Initializer?.Value is LiteralExpressionSyntax lit && lit.IsKind(SyntaxKind.StringLiteralExpression))
                    {
                        state.ConstValues[declarator.Identifier.ValueText] = lit.Token.ValueText;
                    }
                }
            }

            if (hasTestProto)
            {
                foreach (var declarator in field.Declaration.Variables)
                {
                    var raw = ExtractRawText(declarator.Initializer?.Value);
                    if (raw is not null)
                        state.TestPrototypeYaml.Add(raw);
                }
            }

            CollectProtoIdFromField(field, model, state, context.CancellationToken);
        }

        foreach (var prop in root.DescendantNodes().OfType<PropertyDeclarationSyntax>())
        {
            CollectProtoIdFromProperty(prop, model, state, context.CancellationToken);
        }
    }

    private static void CollectProtoIdFromField(FieldDeclarationSyntax field, SemanticModel model, State state, System.Threading.CancellationToken ct)
    {
        if (!IsProtoIdType(field.Declaration.Type, model, ct))
            return;
        foreach (var declarator in field.Declaration.Variables)
        {
            if (declarator.Initializer?.Value is LiteralExpressionSyntax lit && lit.IsKind(SyntaxKind.StringLiteralExpression))
            {
                state.ProtoIdLiterals.Add((lit.Token.ValueText, lit.GetLocation()));
            }
        }
    }

    private static void CollectProtoIdFromProperty(PropertyDeclarationSyntax prop, SemanticModel model, State state, System.Threading.CancellationToken ct)
    {
        if (!IsProtoIdType(prop.Type, model, ct))
            return;
        if (prop.Initializer?.Value is LiteralExpressionSyntax lit && lit.IsKind(SyntaxKind.StringLiteralExpression))
        {
            state.ProtoIdLiterals.Add((lit.Token.ValueText, lit.GetLocation()));
        }
    }

    private static void Report(CompilationAnalysisContext context, State state)
    {
        var testIds = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (var raw in state.TestPrototypeYaml)
        {
            foreach (Match match in IdRegex.Matches(raw))
            {
                var name = match.Groups["name"].Value;
                if (!string.IsNullOrEmpty(name))
                {
                    if (state.ConstValues.TryGetValue(name, out var value))
                        testIds.Add(value);
                }
                else
                {
                    testIds.Add(match.Groups["lit"].Value);
                }
            }
        }

        if (testIds.Count == 0)
            return;

        foreach (var (value, location) in state.ProtoIdLiterals)
        {
            if (!testIds.Contains(value))
                continue;
            if (!IsForkFile(location.SourceTree?.FilePath))
                continue;
            context.ReportDiagnostic(Diagnostic.Create(Descriptor, location, value));
        }
    }

    private static bool IsForkFile(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return false;
        var norm = path!.Replace('\\', '/');
        return norm.Contains("/RussStation/") || norm.EndsWith(".Honk.cs", System.StringComparison.Ordinal);
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

    private static bool HasTestPrototypesAttribute(FieldDeclarationSyntax field, SemanticModel model, System.Threading.CancellationToken ct)
    {
        foreach (var list in field.AttributeLists)
        {
            foreach (var attr in list.Attributes)
            {
                var type = model.GetTypeInfo(attr, ct).Type;
                if (type?.Name == "TestPrototypesAttribute" || type?.Name == "TestPrototypes")
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

    private static bool IsProtoIdType(TypeSyntax? typeSyntax, SemanticModel model, System.Threading.CancellationToken ct)
    {
        if (typeSyntax is null)
            return false;
        var type = model.GetTypeInfo(typeSyntax, ct).Type;
        return type?.Name == "ProtoId";
    }
}
