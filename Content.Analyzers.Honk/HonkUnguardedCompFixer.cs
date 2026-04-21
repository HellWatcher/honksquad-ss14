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
/// Code fix for HONK0011: converts <c>var comp = Comp&lt;T&gt;(args.X);</c>
/// into an early-return TryComp guard in a void method:
/// <c>if (!TryComp&lt;T&gt;(args.X, out var comp)) return;</c>. Only rewrites
/// the local-declaration form inside a <c>void</c> method, so the existing
/// control flow stays intact. Any other shape (expression statement,
/// non-void method, nested use) is left for the human.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp), Shared]
public sealed class HonkUnguardedCompFixer : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create("HONK0011");

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
            return;

        foreach (var diagnostic in context.Diagnostics)
        {
            var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
            var invocation = node.FirstAncestorOrSelf<InvocationExpressionSyntax>();
            if (invocation is null)
                continue;

            var genericName = GetCompGeneric(invocation);
            if (genericName is null || invocation.ArgumentList.Arguments.Count < 1)
                continue;

            var uidArg = invocation.ArgumentList.Arguments[0].Expression;

            // Pattern: `var name = Comp<T>(args.X);` as a sole declarator in a local decl statement
            var localDecl = invocation.FirstAncestorOrSelf<LocalDeclarationStatementSyntax>();
            if (localDecl is null)
                continue;
            if (localDecl.Declaration.Variables.Count != 1)
                continue;
            var declarator = localDecl.Declaration.Variables[0];
            if (declarator.Initializer?.Value != invocation)
                continue;

            var method = invocation.FirstAncestorOrSelf<MethodDeclarationSyntax>();
            if (method is null || !IsVoidReturn(method))
                continue;

            var compName = declarator.Identifier.ValueText;
            var typeArg = genericName.TypeArgumentList.Arguments[0];

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: $"Wrap in if (!TryComp<{typeArg}>(...)) return;",
                    createChangedDocument: c => RewriteAsync(context.Document, localDecl, typeArg, uidArg, compName, c),
                    equivalenceKey: "HonkWrapTryComp"),
                diagnostic);
        }
    }

    private static async Task<Document> RewriteAsync(
        Document document,
        LocalDeclarationStatementSyntax localDecl,
        TypeSyntax typeArg,
        ExpressionSyntax uidArg,
        string compName,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
            return document;

        var tryCompCall = SyntaxFactory.InvocationExpression(
            SyntaxFactory.GenericName(SyntaxFactory.Identifier("TryComp"))
                .WithTypeArgumentList(SyntaxFactory.TypeArgumentList(SyntaxFactory.SingletonSeparatedList(typeArg.WithoutTrivia()))),
            SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(new[]
            {
                SyntaxFactory.Argument(uidArg.WithoutTrivia()),
                SyntaxFactory.Argument(null,
                    SyntaxFactory.Token(SyntaxKind.OutKeyword).WithTrailingTrivia(SyntaxFactory.Space),
                    SyntaxFactory.DeclarationExpression(
                        SyntaxFactory.IdentifierName("var").WithTrailingTrivia(SyntaxFactory.Space),
                        SyntaxFactory.SingleVariableDesignation(SyntaxFactory.Identifier(compName)))),
            })));

        var ifStatement = SyntaxFactory.IfStatement(
                SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, tryCompCall),
                SyntaxFactory.ReturnStatement())
            .WithTriviaFrom(localDecl)
            .WithAdditionalAnnotations(Formatter.Annotation);

        return document.WithSyntaxRoot(root.ReplaceNode(localDecl, ifStatement));
    }

    private static GenericNameSyntax? GetCompGeneric(InvocationExpressionSyntax invocation)
    {
        var generic = invocation.Expression switch
        {
            GenericNameSyntax gn => gn,
            MemberAccessExpressionSyntax m => m.Name as GenericNameSyntax,
            _ => null,
        };
        if (generic?.Identifier.ValueText != "Comp")
            return null;
        if (generic.TypeArgumentList.Arguments.Count == 0)
            return null;
        return generic;
    }

    private static bool IsVoidReturn(MethodDeclarationSyntax method)
    {
        return method.ReturnType is PredefinedTypeSyntax pts && pts.Keyword.IsKind(SyntaxKind.VoidKeyword);
    }
}
