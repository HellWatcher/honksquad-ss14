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
/// Code fix for HONK0012: attaches the auto-state scaffolding to a
/// networked fork component that declares <c>[DataField]</c>s but ships
/// no state mechanism. Adds <c>[AutoGenerateComponentState]</c>, marks
/// the class <c>partial</c>, and inserts <c>using Robust.Shared.GameStates;</c>
/// when absent. Fields still need <c>[AutoNetworkedField]</c> (or a
/// manual state override) to actually replicate; the analyzer will stop
/// firing either way, so the remaining networking work is surfaced in
/// code review rather than by the rule.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp), Shared]
public sealed class HonkNetworkedComponentStateFixer : CodeFixProvider
{
    private const string AutoGenerateAttributeName = "AutoGenerateComponentState";
    private const string GameStatesNamespace = "Robust.Shared.GameStates";

    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create("HONK0012");

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
            return;

        foreach (var diagnostic in context.Diagnostics)
        {
            var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
            var cls = node.FirstAncestorOrSelf<ClassDeclarationSyntax>();
            if (cls is null)
                continue;

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Add [AutoGenerateComponentState] and mark partial",
                    createChangedDocument: c => ApplyFixAsync(context.Document, cls, c),
                    equivalenceKey: "HonkAddAutoGenerateComponentState"),
                diagnostic);
        }
    }

    private static async Task<Document> ApplyFixAsync(Document document, ClassDeclarationSyntax cls, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is not CompilationUnitSyntax unit)
            return document;

        var updatedClass = cls;

        if (!HasAttribute(updatedClass, AutoGenerateAttributeName))
        {
            var attribute = SyntaxFactory.Attribute(SyntaxFactory.IdentifierName(AutoGenerateAttributeName));
            var list = SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(attribute))
                .WithAdditionalAnnotations(Formatter.Annotation);
            updatedClass = updatedClass.AddAttributeLists(list);
        }

        if (!updatedClass.Modifiers.Any(SyntaxKind.PartialKeyword))
        {
            var partialToken = SyntaxFactory.Token(SyntaxKind.PartialKeyword)
                .WithTrailingTrivia(SyntaxFactory.Space);
            updatedClass = updatedClass.WithModifiers(updatedClass.Modifiers.Add(partialToken))
                .WithAdditionalAnnotations(Formatter.Annotation);
        }

        var newUnit = unit.ReplaceNode(cls, updatedClass);

        if (!HasUsing(newUnit, GameStatesNamespace))
        {
            var usingDirective = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(GameStatesNamespace))
                .WithAdditionalAnnotations(Formatter.Annotation);
            newUnit = newUnit.AddUsings(usingDirective);
        }

        return document.WithSyntaxRoot(newUnit);
    }

    private static bool HasAttribute(ClassDeclarationSyntax cls, string name)
    {
        foreach (var list in cls.AttributeLists)
        {
            foreach (var attr in list.Attributes)
            {
                var attrName = attr.Name switch
                {
                    IdentifierNameSyntax id => id.Identifier.ValueText,
                    QualifiedNameSyntax q => q.Right.Identifier.ValueText,
                    _ => null,
                };
                if (attrName == name || attrName == name + "Attribute")
                    return true;
            }
        }
        return false;
    }

    private static bool HasUsing(CompilationUnitSyntax unit, string ns)
    {
        foreach (var u in unit.Usings)
        {
            if (u.Name?.ToString() == ns)
                return true;
        }
        return false;
    }
}
