using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Content.Analyzers.Honk;

/// <summary>
/// HONK0016: a fork-owned <c>[DataField]</c> member initialized with a raw
/// numeric literal bakes the value into source instead of referencing a
/// named constant. The magic-numbers audit moved every such literal into a
/// per-system <c>&lt;System&gt;Constants.cs</c> file; this analyzer stops
/// the regression. Scoped to fork files (path contains <c>/RussStation/</c>
/// or ends in <c>.Honk.cs</c>).
///
/// Covers:
///   - Direct numeric literal initializers (<c>= 0.5f</c>, <c>= -100f</c>, <c>= 10</c>).
///   - Numeric literals inside collection/array initializers on the DataField
///     (<c>= [1f, 3f, 6f]</c>, <c>= new float[] { 1f, 3f, 6f }</c>,
///     <c>= new[] { 1f, 3f, 6f }</c>).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HonkMagicNumberAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Descriptor = new(
        id: "HONK0016",
        title: "Magic numeric literal in [DataField] default",
        messageFormat: "[DataField] '{0}' is initialized with a raw numeric literal ({1}); move the value to a per-system *Constants.cs and reference it here",
        category: "Honk.Readability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "DataField defaults are the dominant source of fork magic numbers. Every numeric default must reference a named constant in a per-system constants file so reviewers can trace the value's meaning and retune centrally.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeField, SyntaxKind.FieldDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeProperty, SyntaxKind.PropertyDeclaration);
    }

    private static void AnalyzeField(SyntaxNodeAnalysisContext context)
    {
        var field = (FieldDeclarationSyntax)context.Node;

        if (!IsForkFile(field.SyntaxTree.FilePath))
            return;

        if (!HasDataFieldAttribute(field.AttributeLists))
            return;

        foreach (var variable in field.Declaration.Variables)
        {
            var initializer = variable.Initializer;
            if (initializer is null)
                continue;

            ReportIfMagic(context, variable.Identifier.Text, initializer.Value);
        }
    }

    private static void AnalyzeProperty(SyntaxNodeAnalysisContext context)
    {
        var property = (PropertyDeclarationSyntax)context.Node;

        if (!IsForkFile(property.SyntaxTree.FilePath))
            return;

        if (!HasDataFieldAttribute(property.AttributeLists))
            return;

        var initializer = property.Initializer;
        if (initializer is null)
            return;

        ReportIfMagic(context, property.Identifier.Text, initializer.Value);
    }

    private static bool IsForkFile(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        return path!.Replace('\\', '/').Contains("/RussStation/")
            || path.EndsWith(".Honk.cs", StringComparison.Ordinal);
    }

    private static bool HasDataFieldAttribute(SyntaxList<AttributeListSyntax> attributeLists)
    {
        foreach (var list in attributeLists)
        {
            foreach (var attr in list.Attributes)
            {
                if (IsDataFieldName(attr.Name))
                    return true;
            }
        }
        return false;
    }

    private static bool IsDataFieldName(NameSyntax name)
    {
        var text = name switch
        {
            SimpleNameSyntax simple => simple.Identifier.Text,
            QualifiedNameSyntax qualified => qualified.Right.Identifier.Text,
            _ => null,
        };

        return text is "DataField" or "DataFieldAttribute";
    }

    private static void ReportIfMagic(SyntaxNodeAnalysisContext context, string memberName, ExpressionSyntax value)
    {
        // Direct literal or unary-minus literal.
        if (TryUnwrapNumericLiteral(value) is { } directText)
        {
            Report(context, memberName, value, directText);
            return;
        }

        // Collection expression: = [1f, 3f, 6f]
        if (value is CollectionExpressionSyntax collection)
        {
            foreach (var element in collection.Elements)
            {
                if (element is ExpressionElementSyntax ee
                    && TryUnwrapNumericLiteral(ee.Expression) is { } elemText)
                {
                    Report(context, memberName, ee.Expression, elemText);
                }
            }
            return;
        }

        // Explicit array creation: = new float[] { 1f, 3f, 6f }
        if (value is ArrayCreationExpressionSyntax array && array.Initializer is { } arrayInit)
        {
            FlagInitializer(context, memberName, arrayInit);
            return;
        }

        // Implicit array creation: = new[] { 1f, 3f, 6f }
        if (value is ImplicitArrayCreationExpressionSyntax implicitArray)
        {
            FlagInitializer(context, memberName, implicitArray.Initializer);
            return;
        }
    }

    private static void FlagInitializer(SyntaxNodeAnalysisContext context, string memberName, InitializerExpressionSyntax initializer)
    {
        foreach (var expr in initializer.Expressions)
        {
            if (TryUnwrapNumericLiteral(expr) is { } text)
                Report(context, memberName, expr, text);
        }
    }

    private static void Report(SyntaxNodeAnalysisContext context, string memberName, ExpressionSyntax location, string literalText)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            Descriptor,
            location.GetLocation(),
            memberName,
            literalText));
    }

    internal static string? TryUnwrapNumericLiteral(ExpressionSyntax expr)
    {
        switch (expr)
        {
            case LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.NumericLiteralExpression):
                return literal.Token.Text;
            case PrefixUnaryExpressionSyntax unary when unary.IsKind(SyntaxKind.UnaryMinusExpression)
                                                       && unary.Operand is LiteralExpressionSyntax inner
                                                       && inner.IsKind(SyntaxKind.NumericLiteralExpression):
                return "-" + inner.Token.Text;
            default:
                return null;
        }
    }
}
