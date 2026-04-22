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

namespace Content.Analyzers.Honk;

/// <summary>
/// Code fix for HONK0005: wraps the flagged
/// <c>[Access(typeof(ForkSystem))]</c> in <c>// HONK START</c> /
/// <c>// HONK END</c> marker comments so rebase can see the fork-specific
/// friend. Only engages when the attribute is alone in its list; lists
/// with multiple attributes need a human to decide whether to split them.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp), Shared]
public sealed class HonkAccessForkFriendFixer : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create("HONK0005");

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
            return;

        foreach (var diagnostic in context.Diagnostics)
        {
            var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
            var attribute = node.FirstAncestorOrSelf<AttributeSyntax>();
            if (attribute?.Parent is not AttributeListSyntax list)
                continue;

            // Multi-attribute lists like `[A, B]` need a human to decide whether
            // to split them; skip the fix rather than guess.
            if (list.Attributes.Count != 1)
                continue;

            // If the attribute list is already inside a HONK block, leave it alone.
            var text = list.SyntaxTree.GetText(context.CancellationToken);
            var blocks = HonkMarkerBlocks.Find(text);
            if (HonkMarkerBlocks.Contains(blocks, list.Span))
                continue;

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Wrap in // HONK START / // HONK END",
                    createChangedDocument: c => WrapAsync(context.Document, list, c),
                    equivalenceKey: "HonkAccessForkFriendWrap"),
                diagnostic);
        }
    }

    private static async Task<Document> WrapAsync(Document document, AttributeListSyntax list, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
            return document;

        var indent = ExtractIndent(list.GetLeadingTrivia());

        var newLeading = list.GetLeadingTrivia()
            .Add(SyntaxFactory.Comment("// HONK START"))
            .Add(SyntaxFactory.EndOfLine("\n"));
        if (indent.Length > 0)
            newLeading = newLeading.Add(SyntaxFactory.Whitespace(indent));

        var newTrailing = SyntaxFactory.TriviaList(
            SyntaxFactory.EndOfLine("\n"));
        if (indent.Length > 0)
            newTrailing = newTrailing.Add(SyntaxFactory.Whitespace(indent));
        newTrailing = newTrailing
            .Add(SyntaxFactory.Comment("// HONK END"))
            .AddRange(list.GetTrailingTrivia());

        var replacement = list
            .WithLeadingTrivia(newLeading)
            .WithTrailingTrivia(newTrailing);

        return document.WithSyntaxRoot(root.ReplaceNode(list, replacement));
    }

    private static string ExtractIndent(SyntaxTriviaList leading)
    {
        // The indent is the whitespace between the last newline in the leading
        // trivia (or the start of the trivia) and the attribute's `[`.
        var lastWhitespace = string.Empty;
        foreach (var trivia in leading)
        {
            if (trivia.IsKind(SyntaxKind.EndOfLineTrivia))
            {
                lastWhitespace = string.Empty;
                continue;
            }
            if (trivia.IsKind(SyntaxKind.WhitespaceTrivia))
            {
                lastWhitespace = trivia.ToString();
            }
            else
            {
                lastWhitespace = string.Empty;
            }
        }
        return lastWhitespace;
    }
}
