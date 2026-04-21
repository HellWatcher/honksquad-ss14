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
/// Code fix for HONK0016: extracts a magic numeric literal on a
/// <c>[DataField]</c> into a sibling <c>private const</c> named
/// <c>{FieldName}Default</c> and replaces the literal with a reference.
/// Keeps the fix local to the containing type; moving the constant to a
/// per-system <c>*Constants.cs</c> file remains a manual follow-up.
/// Only handles direct numeric literals (including unary-minus); array
/// and collection initializers still need manual extraction.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp), Shared]
public sealed class HonkMagicNumberFixer : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("HONK0016");

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

            var member = literal.FirstAncestorOrSelf<MemberDeclarationSyntax>(m =>
                m is FieldDeclarationSyntax or PropertyDeclarationSyntax);

            if (member is null)
                continue;

            if (!TryGetMemberInfo(member, out var memberName, out var memberType))
                continue;

            // Only rewrite when the flagged literal IS the initializer value itself —
            // array/collection initializers stay a manual extraction.
            if (GetInitializerValue(member) != literal)
                continue;

            var typeDecl = member.Parent as TypeDeclarationSyntax;
            if (typeDecl is null)
                continue;

            var constName = memberName + "Default";
            if (TypeAlreadyHasMember(typeDecl, constName))
                continue;

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: $"Extract to private const '{constName}'",
                    createChangedDocument: c => ExtractConstAsync(context.Document, typeDecl, member, literal, constName, memberType, c),
                    equivalenceKey: "HonkExtractConst"),
                diagnostic);
        }
    }

    private static async Task<Document> ExtractConstAsync(
        Document document,
        TypeDeclarationSyntax typeDecl,
        MemberDeclarationSyntax dataFieldMember,
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
                constType.WithoutTrivia(),
                SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.VariableDeclarator(
                        identifier: SyntaxFactory.Identifier(constName),
                        argumentList: null,
                        initializer: SyntaxFactory.EqualsValueClause(literal.WithoutTrivia())))))
            .WithAdditionalAnnotations(Formatter.Annotation);

        var replacement = SyntaxFactory.IdentifierName(constName).WithTriviaFrom(literal);

        var editor = new TrackedEditor(root);
        editor.TrackNodes(dataFieldMember, literal, typeDecl);

        root = editor.ReplaceNode(literal, replacement);
        root = editor.InsertBefore(dataFieldMember, constDecl);

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

    private static bool TryGetMemberInfo(MemberDeclarationSyntax member, out string name, out TypeSyntax type)
    {
        switch (member)
        {
            case FieldDeclarationSyntax field when field.Declaration.Variables.Count == 1:
                name = field.Declaration.Variables[0].Identifier.Text;
                type = field.Declaration.Type;
                return true;
            case PropertyDeclarationSyntax prop:
                name = prop.Identifier.Text;
                type = prop.Type;
                return true;
            default:
                name = string.Empty;
                type = null!;
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

    /// <summary>
    /// Tiny wrapper around <see cref="SyntaxNode.TrackNodes"/> so the two
    /// edits (replace literal, insert const) can reference the same
    /// original nodes after the first replacement changes identity.
    /// </summary>
    private sealed class TrackedEditor
    {
        private SyntaxNode _root;

        public TrackedEditor(SyntaxNode root)
        {
            _root = root;
        }

        public void TrackNodes(params SyntaxNode[] nodes)
        {
            _root = _root.TrackNodes(nodes);
        }

        public SyntaxNode ReplaceNode(SyntaxNode original, SyntaxNode replacement)
        {
            var current = _root.GetCurrentNode(original);
            if (current is null)
                return _root;
            _root = _root.ReplaceNode(current, replacement);
            return _root;
        }

        public SyntaxNode InsertBefore(SyntaxNode anchor, SyntaxNode toInsert)
        {
            var current = _root.GetCurrentNode(anchor);
            if (current is null)
                return _root;
            _root = _root.InsertNodesBefore(current, new[] { toInsert });
            return _root;
        }
    }
}
