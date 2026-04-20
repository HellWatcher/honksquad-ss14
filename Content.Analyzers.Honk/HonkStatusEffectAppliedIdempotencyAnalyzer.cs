using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Content.Analyzers.Honk;

/// <summary>
/// HONK0013: a <c>SubscribeLocalEvent&lt;TComp, StatusEffectAppliedEvent&gt;</c>
/// handler must short-circuit on <c>IGameTiming.ApplyingState</c>. The
/// <c>StatusEffectComponent.Applied</c> field is intentionally not networked,
/// so client state replay re-raises the event every tick; a non-idempotent
/// handler will run repeatedly instead of once per real application.
/// Scoped to fork files (path contains <c>/RussStation/</c> or ends in
/// <c>.Honk.cs</c>) so upstream drift is not this rule's problem.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HonkStatusEffectAppliedIdempotencyAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Descriptor = new(
        id: "HONK0013",
        title: "StatusEffectAppliedEvent handler missing ApplyingState guard",
        messageFormat: "Handler '{0}' for StatusEffectAppliedEvent does not short-circuit on IGameTiming.ApplyingState; it will re-run every time the client re-applies the component's state",
        category: "Honk.StatusEffect",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "StatusEffectComponent.Applied is not networked, so client state replay re-fires StatusEffectAppliedEvent continuously. Handlers that do non-idempotent work must early-out when IGameTiming.ApplyingState is true. See SleepingSystem for the canonical guard.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (!IsForkFile(invocation.SyntaxTree.FilePath))
            return;

        if (!TryGetSubscribeLocalEventName(invocation, out var nameSyntax))
            return;

        if (nameSyntax!.TypeArgumentList.Arguments.Count < 2)
            return;

        var eventTypeSyntax = nameSyntax.TypeArgumentList.Arguments[1];
        var eventType = context.SemanticModel.GetTypeInfo(eventTypeSyntax, context.CancellationToken).Type;
        if (eventType is null || eventType.Name != "StatusEffectAppliedEvent")
            return;

        if (invocation.ArgumentList.Arguments.Count == 0)
            return;

        var handlerExpression = invocation.ArgumentList.Arguments[0].Expression;

        SyntaxNode? body;
        string handlerName;

        if (handlerExpression is AnonymousFunctionExpressionSyntax anon)
        {
            handlerName = "<lambda>";
            body = anon.Body;
        }
        else if (context.SemanticModel.GetSymbolInfo(handlerExpression, context.CancellationToken).Symbol is IMethodSymbol handlerSymbol)
        {
            handlerName = handlerSymbol.Name;
            var reference = handlerSymbol.DeclaringSyntaxReferences.FirstOrDefault();
            if (reference is null)
                return;
            body = reference.GetSyntax(context.CancellationToken);
        }
        else
        {
            return;
        }

        if (body is null)
            return;

        if (BodyReferencesApplyingState(body))
            return;

        context.ReportDiagnostic(Diagnostic.Create(Descriptor, handlerExpression.GetLocation(), handlerName));
    }

    private static bool IsForkFile(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        return path!.Replace('\\', '/').Contains("/RussStation/")
            || path.EndsWith(".Honk.cs", StringComparison.Ordinal);
    }

    private static bool TryGetSubscribeLocalEventName(InvocationExpressionSyntax invocation, out GenericNameSyntax? name)
    {
        name = invocation.Expression switch
        {
            MemberAccessExpressionSyntax ma => ma.Name as GenericNameSyntax,
            GenericNameSyntax g => g,
            _ => null,
        };

        if (name is null)
            return false;

        return name.Identifier.ValueText == "SubscribeLocalEvent";
    }

    private static bool BodyReferencesApplyingState(SyntaxNode body)
    {
        foreach (var id in body.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            if (id.Identifier.ValueText == "ApplyingState")
                return true;
        }
        return false;
    }
}
