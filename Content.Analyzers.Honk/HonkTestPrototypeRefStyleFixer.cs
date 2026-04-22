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
/// Code fix for HONK0014: rewrites a flagged
/// <c>ProtoId&lt;T&gt; Name = "literal";</c> field into
/// <c>const string Name = "literal";</c>. ProtoId&lt;T&gt; has an implicit
/// conversion from string, so assignment sites keep compiling; direct
/// <c>.Id</c> access is rare and left as a follow-up when it breaks.
/// Properties are out of scope since a property cannot be <c>const</c>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp), Shared]
public sealed class HonkTestPrototypeRefStyleFixer : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create("HONK0014");

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
            return;

        foreach (var diagnostic in context.Diagnostics)
        {
            var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
            var field = node.FirstAncestorOrSelf<FieldDeclarationSyntax>();
            if (field is null)
                continue;
            if (field.Declaration.Variables.Count != 1)
                continue;

            var declarator = field.Declaration.Variables[0];
            if (declarator.Initializer?.Value is not LiteralExpressionSyntax lit
                || !lit.IsKind(SyntaxKind.StringLiteralExpression))
                continue;

            if (field.Modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword)))
                continue;

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Convert to 'const string'",
                    createChangedDocument: c => RewriteAsync(context.Document, field, c),
                    equivalenceKey: "HonkTestProtoConstString"),
                diagnostic);
        }
    }

    private static async Task<Document> RewriteAsync(
        Document document,
        FieldDeclarationSyntax field,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
            return document;

        var keptModifiers = field.Modifiers
            .Where(m => !m.IsKind(SyntaxKind.ReadOnlyKeyword)
                        && !m.IsKind(SyntaxKind.StaticKeyword)
                        && !m.IsKind(SyntaxKind.VolatileKeyword))
            .ToList();

        var constToken = SyntaxFactory.Token(SyntaxKind.ConstKeyword);
        var insertIndex = keptModifiers.FindIndex(m => !IsVisibilityModifier(m));
        if (insertIndex < 0)
            insertIndex = keptModifiers.Count;
        keptModifiers.Insert(insertIndex, constToken);

        var newModifiers = SyntaxFactory.TokenList(keptModifiers);
        if (newModifiers.Count > 0)
            newModifiers = newModifiers.Replace(newModifiers[0], newModifiers[0].WithLeadingTrivia(field.Modifiers.Count > 0 ? field.Modifiers[0].LeadingTrivia : field.GetLeadingTrivia()));

        var stringType = SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword))
            .WithTriviaFrom(field.Declaration.Type);

        var newDeclaration = field.Declaration.WithType(stringType);
        var newField = field
            .WithModifiers(newModifiers)
            .WithDeclaration(newDeclaration)
            .WithAdditionalAnnotations(Formatter.Annotation);

        return document.WithSyntaxRoot(root.ReplaceNode(field, newField));
    }

    private static bool IsVisibilityModifier(SyntaxToken token)
    {
        return token.IsKind(SyntaxKind.PublicKeyword)
               || token.IsKind(SyntaxKind.PrivateKeyword)
               || token.IsKind(SyntaxKind.ProtectedKeyword)
               || token.IsKind(SyntaxKind.InternalKeyword);
    }
}
