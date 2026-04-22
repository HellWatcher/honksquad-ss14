using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace Content.Analyzers.Honk;

/// <summary>
/// Code fix for HONK0017: extracts a flagged numeric literal inside a
/// field or property initializer's layout/time constructor argument
/// into a sibling <c>private const</c> named <c>{MemberName}Default</c>.
/// Deliberately narrow: only engages when the enclosing initializer
/// contains exactly one numeric literal, so repeated runs and FixAll
/// cannot race into colliding const names. Multi-literal call sites
/// (e.g. <c>new Thickness(4f, 8f)</c>) stay flagged for a human pick,
/// which is where the HONK0017 backlog note about "needs a naming
/// heuristic" lives.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp), Shared]
public sealed class HonkLayoutMagicNumberFixer : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create("HONK0017");

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
            return;

        foreach (var diagnostic in context.Diagnostics)
        {
            var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
            var literal = node.AncestorsAndSelf()
                .FirstOrDefault(a => a is LiteralExpressionSyntax or PrefixUnaryExpressionSyntax) as ExpressionSyntax;
            if (literal is null || !IsDirectNumericLiteral(literal))
                continue;

            var argList = literal.FirstAncestorOrSelf<ArgumentListSyntax>();
            if (argList is null)
                continue;
            if (CountNumericLiterals(argList) != 1)
                continue;

            var member = literal.FirstAncestorOrSelf<MemberDeclarationSyntax>(m =>
                m is FieldDeclarationSyntax or PropertyDeclarationSyntax);
            if (member is null)
                continue;

            if (!TryGetMemberName(member, out var memberName))
                continue;

            if (GetInitializerValue(member) is not { } initializerValue)
                continue;

            // Only rewrite when the literal sits inside the initializer expression,
            // not in some unrelated nested context (e.g. an attribute argument).
            if (!initializerValue.Span.Contains(literal.Span))
                continue;

            var typeDecl = member.Parent as TypeDeclarationSyntax;
            if (typeDecl is null)
                continue;

            var constName = memberName + "Default";
            if (TypeAlreadyHasMember(typeDecl, constName))
                continue;

            var constType = InferLiteralType(literal);
            if (constType is null)
                continue;

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: $"Extract to private const '{constName}'",
                    createChangedDocument: c => ExtractConstAsync(context.Document, typeDecl, member, literal, constName, constType, c),
                    equivalenceKey: "HonkLayoutExtractConst"),
                diagnostic);
        }
    }

    private static async Task<Document> ExtractConstAsync(
        Document document,
        TypeDeclarationSyntax typeDecl,
        MemberDeclarationSyntax member,
        ExpressionSyntax literal,
        string constName,
        TypeSyntax constType,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
            return document;

        var constDecl = SyntaxFactory.FieldDeclaration(
            attributeLists: default,
            modifiers: SyntaxFactory.TokenList(
                SyntaxFactory.Token(SyntaxKind.PrivateKeyword),
                SyntaxFactory.Token(SyntaxKind.ConstKeyword)),
            declaration: SyntaxFactory.VariableDeclaration(
                constType,
                SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.VariableDeclarator(
                        identifier: SyntaxFactory.Identifier(constName),
                        argumentList: null,
                        initializer: SyntaxFactory.EqualsValueClause(literal.WithoutTrivia())))))
            .WithAdditionalAnnotations(Formatter.Annotation);

        var replacement = SyntaxFactory.IdentifierName(constName).WithTriviaFrom(literal);

        root = root.TrackNodes(member, literal);
        var trackedLiteral = root.GetCurrentNode(literal);
        if (trackedLiteral is not null)
            root = root.ReplaceNode(trackedLiteral, replacement);

        var trackedMember = root.GetCurrentNode(member);
        if (trackedMember is not null)
            root = root.InsertNodesBefore(trackedMember, new[] { constDecl });

        return document.WithSyntaxRoot(root);
    }

    private static bool IsDirectNumericLiteral(ExpressionSyntax expr)
    {
        return expr switch
        {
            LiteralExpressionSyntax l => l.IsKind(SyntaxKind.NumericLiteralExpression),
            PrefixUnaryExpressionSyntax u => u.IsKind(SyntaxKind.UnaryMinusExpression)
                                             && u.Operand is LiteralExpressionSyntax inner
                                             && inner.IsKind(SyntaxKind.NumericLiteralExpression),
            _ => false,
        };
    }

    private static int CountNumericLiterals(ArgumentListSyntax args)
    {
        var count = 0;
        foreach (var arg in args.Arguments)
        {
            if (IsDirectNumericLiteral(arg.Expression))
                count++;
        }
        return count;
    }

    private static bool TryGetMemberName(MemberDeclarationSyntax member, out string name)
    {
        switch (member)
        {
            case FieldDeclarationSyntax field when field.Declaration.Variables.Count == 1:
                name = field.Declaration.Variables[0].Identifier.Text;
                return true;
            case PropertyDeclarationSyntax prop:
                name = prop.Identifier.Text;
                return true;
            default:
                name = string.Empty;
                return false;
        }
    }

    private static ExpressionSyntax? GetInitializerValue(MemberDeclarationSyntax member) => member switch
    {
        FieldDeclarationSyntax f when f.Declaration.Variables.Count == 1 => f.Declaration.Variables[0].Initializer?.Value,
        PropertyDeclarationSyntax p => p.Initializer?.Value,
        _ => null,
    };

    private static bool TypeAlreadyHasMember(TypeDeclarationSyntax typeDecl, string name)
    {
        foreach (var m in typeDecl.Members)
        {
            switch (m)
            {
                case FieldDeclarationSyntax f:
                    foreach (var v in f.Declaration.Variables)
                        if (v.Identifier.Text == name)
                            return true;
                    break;
                case PropertyDeclarationSyntax p:
                    if (p.Identifier.Text == name)
                        return true;
                    break;
            }
        }
        return false;
    }

    private static TypeSyntax? InferLiteralType(ExpressionSyntax literal)
    {
        var token = literal switch
        {
            LiteralExpressionSyntax l => l.Token,
            PrefixUnaryExpressionSyntax { Operand: LiteralExpressionSyntax l } => l.Token,
            _ => default,
        };
        if (token == default)
            return null;

        var text = token.Text;
        if (text.Length == 0)
            return null;

        var last = char.ToLowerInvariant(text[text.Length - 1]);
        var kind = last switch
        {
            'f' => SyntaxKind.FloatKeyword,
            'd' => SyntaxKind.DoubleKeyword,
            'm' => SyntaxKind.DecimalKeyword,
            'l' => SyntaxKind.LongKeyword,
            _ => text.Contains('.') || text.Contains('e') || text.Contains('E')
                ? SyntaxKind.DoubleKeyword
                : SyntaxKind.IntKeyword,
        };

        return SyntaxFactory.PredefinedType(SyntaxFactory.Token(kind));
    }
}
