using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Content.Analyzers.Honk;

/// <summary>
/// HONK0017: a fork-owned call site passing a raw numeric literal to a known
/// UI-layout constructor (<c>Thickness</c>, <c>Vector2/3/4</c>, <c>UIBox2</c>,
/// <c>Box2</c>, <c>Color</c>) or to one of the <c>TimeSpan.FromX</c> factory
/// methods bakes a layout decision or duration into source instead of
/// referencing a named constant. Pairs with HONK0016, which covers DataField
/// default values; this rule covers the call-site-argument case that appears
/// everywhere UI widgets, coordinate offsets, and durations are constructed.
///
/// Scoped to fork files (path contains <c>/RussStation/</c> or ends in
/// <c>.Honk.cs</c>). Exempts call sites inside a <c>const</c> or
/// <c>static readonly</c> field initializer — those declarations *are* the
/// named constant, so the literal at the defining site is allowed.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HonkLayoutMagicNumberAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Descriptor = new(
        id: "HONK0017",
        title: "Magic numeric literal in layout/time constructor argument",
        messageFormat: "'{0}' is called with a raw numeric literal ({1}) at argument {2}; move the value to a per-system *Constants.cs and reference it here",
        category: "Honk.Readability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "UI layout decisions (Thickness corners, Vector axes, Color channels, UIBox/Box corners) and durations (TimeSpan.FromX arguments) should live in a per-system constants file so each axis/side/channel is a distinct, retunable named value and reviewers can spot drift across call sites.");

    // Type names that take magic-number-prone constructor arguments. Matched by
    // the rightmost identifier in the type syntax — so "Thickness" matches
    // `Thickness`, `Robust.Shared.Maths.Thickness`, `new Thickness(...)`, etc.
    private static readonly HashSet<string> LayoutTypes = new(StringComparer.Ordinal)
    {
        "Thickness",
        "Vector2", "Vector3", "Vector4",
        "Vector2i", "Vector3i", "Vector4i",
        "UIBox2", "UIBox2i",
        "Box2", "Box2i",
        "Color",
    };

    private static readonly HashSet<string> TimeSpanFactories = new(StringComparer.Ordinal)
    {
        "FromSeconds", "FromMilliseconds", "FromMinutes", "FromHours", "FromDays", "FromTicks",
    };

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
    {
        var creation = (ObjectCreationExpressionSyntax)context.Node;

        if (!IsForkFile(creation.SyntaxTree.FilePath))
            return;

        if (IsInsideNamedConstantDeclaration(creation))
            return;

        var typeName = GetRightmostIdentifier(creation.Type);
        if (typeName is null || !LayoutTypes.Contains(typeName))
            return;

        if (creation.ArgumentList is not { } args)
            return;

        FlagNumericArguments(context, typeName, args);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (!IsForkFile(invocation.SyntaxTree.FilePath))
            return;

        if (IsInsideNamedConstantDeclaration(invocation))
            return;

        // Match TimeSpan.FromX(...).
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return;

        if (memberAccess.Expression is not IdentifierNameSyntax typeIdent
            || typeIdent.Identifier.Text != "TimeSpan")
        {
            return;
        }

        var methodName = memberAccess.Name.Identifier.Text;
        if (!TimeSpanFactories.Contains(methodName))
            return;

        FlagNumericArguments(context, $"TimeSpan.{methodName}", invocation.ArgumentList);
    }

    private static void FlagNumericArguments(SyntaxNodeAnalysisContext context, string callName, ArgumentListSyntax args)
    {
        for (var i = 0; i < args.Arguments.Count; i++)
        {
            var argExpr = args.Arguments[i].Expression;
            if (TryUnwrapNumericLiteral(argExpr) is not { } literalText)
                continue;

            context.ReportDiagnostic(Diagnostic.Create(
                Descriptor,
                argExpr.GetLocation(),
                callName,
                literalText,
                // Human-friendly 1-based argument index.
                i + 1));
        }
    }

    private static bool IsForkFile(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        return path!.Replace('\\', '/').Contains("/RussStation/")
            || path.EndsWith(".Honk.cs", StringComparison.Ordinal);
    }

    private static string? GetRightmostIdentifier(TypeSyntax type)
    {
        return type switch
        {
            IdentifierNameSyntax ident => ident.Identifier.Text,
            GenericNameSyntax generic => generic.Identifier.Text,
            QualifiedNameSyntax qualified => GetRightmostIdentifier(qualified.Right),
            AliasQualifiedNameSyntax aliased => GetRightmostIdentifier(aliased.Name),
            _ => null,
        };
    }

    private static bool IsInsideNamedConstantDeclaration(SyntaxNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (current is FieldDeclarationSyntax field)
            {
                var hasConst = false;
                var hasStatic = false;
                var hasReadOnly = false;
                foreach (var modifier in field.Modifiers)
                {
                    if (modifier.IsKind(SyntaxKind.ConstKeyword)) hasConst = true;
                    else if (modifier.IsKind(SyntaxKind.StaticKeyword)) hasStatic = true;
                    else if (modifier.IsKind(SyntaxKind.ReadOnlyKeyword)) hasReadOnly = true;
                }
                return hasConst || (hasStatic && hasReadOnly);
            }
            if (current is MethodDeclarationSyntax or ConstructorDeclarationSyntax
                or AccessorDeclarationSyntax or LocalFunctionStatementSyntax)
            {
                // Escaped field-initializer scope without seeing a const/static-readonly field.
                return false;
            }
        }
        return false;
    }

    // Duplicated from HonkMagicNumberAnalyzer so HONK0017 is self-contained —
    // the two analyzers ship in independent PRs. If both land, the helper can
    // be factored into a shared internal class in a follow-up.
    private static string? TryUnwrapNumericLiteral(ExpressionSyntax expr)
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
