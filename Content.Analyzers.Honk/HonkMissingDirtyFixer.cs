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
/// Code fix for HONK0010: inserts <c>Dirty(uid, comp);</c> after a
/// flagged networked-component write, where <c>uid</c> is the first
/// <c>EntityUid</c> parameter of the enclosing method and <c>comp</c>
/// is the receiver of the write's member-access. Bails out when there
/// is no <c>EntityUid</c> parameter in scope, since guessing a name is
/// worse than leaving the warning up for a human to resolve.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp), Shared]
public sealed class HonkMissingDirtyFixer : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create("HONK0010");

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null || model is null)
            return;

        foreach (var diagnostic in context.Diagnostics)
        {
            var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
            var assignment = node.FirstAncestorOrSelf<AssignmentExpressionSyntax>();
            if (assignment?.Left is not MemberAccessExpressionSyntax memberAccess)
                continue;

            var statement = assignment.FirstAncestorOrSelf<ExpressionStatementSyntax>();
            if (statement is null)
                continue;

            var method = assignment.FirstAncestorOrSelf<BaseMethodDeclarationSyntax>();
            if (method is null)
                continue;

            if (!TryGetEntityUidParameter(method, model, context.CancellationToken, out var uidName))
                continue;

            var compExpression = memberAccess.Expression;

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: $"Insert Dirty({uidName}, {compExpression}) after write",
                    createChangedDocument: c => InsertDirtyAsync(context.Document, statement, uidName, compExpression, c),
                    equivalenceKey: "HonkInsertDirty"),
                diagnostic);
        }
    }

    private static bool TryGetEntityUidParameter(BaseMethodDeclarationSyntax method, SemanticModel model, CancellationToken ct, out string name)
    {
        name = string.Empty;
        if (method.ParameterList is null)
            return false;

        foreach (var parameter in method.ParameterList.Parameters)
        {
            if (parameter.Type is null)
                continue;
            var type = model.GetTypeInfo(parameter.Type, ct).Type;
            if (type?.Name == "EntityUid")
            {
                name = parameter.Identifier.ValueText;
                return true;
            }
        }
        return false;
    }

    private static async Task<Document> InsertDirtyAsync(
        Document document,
        ExpressionStatementSyntax writeStatement,
        string uidName,
        ExpressionSyntax compExpression,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
            return document;

        var dirtyCall = SyntaxFactory.ExpressionStatement(
                SyntaxFactory.InvocationExpression(
                    SyntaxFactory.IdentifierName("Dirty"),
                    SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(new[]
                    {
                        SyntaxFactory.Argument(SyntaxFactory.IdentifierName(uidName)),
                        SyntaxFactory.Argument(compExpression.WithoutTrivia()),
                    }))))
            .WithAdditionalAnnotations(Formatter.Annotation);

        var newRoot = root.InsertNodesAfter(writeStatement, new[] { dirtyCall });
        return document.WithSyntaxRoot(newRoot);
    }
}
