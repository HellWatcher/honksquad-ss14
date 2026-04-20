using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Content.Analyzers.Honk;

/// <summary>
/// HONK0009: <c>SubscribeLocalEvent</c>, <c>SubscribeNetworkEvent</c>, and
/// <c>SubscribeAllEvent</c> on <c>EntitySystem</c> must be called from
/// <c>Initialize</c> or an <c>InitializeXxx</c> partial helper invoked by
/// it. Subscribing later registers handlers after earlier events have
/// already fired, and the failure mode is invisible at runtime.
/// Scoped to fork files (path contains <c>/RussStation/</c> or ends in
/// <c>.Honk.cs</c>) so upstream drift is not this rule's problem.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HonkSubscribeOutsideInitializeAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Descriptor = new(
        id: "HONK0009",
        title: "EntitySystem event subscription outside Initialize()",
        messageFormat: "'{0}' is called outside Initialize(); move the subscription into Initialize() or an InitializeXxx helper invoked by it",
        category: "Honk.EntitySystem",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "EntitySystem event subscriptions must be registered from Initialize(). Subscribing later means earlier events are lost and the failure is silent.");

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

        var name = GetInvokedName(invocation);
        if (name is not ("SubscribeLocalEvent" or "SubscribeNetworkEvent" or "SubscribeAllEvent"))
            return;

        // Ignore subscriptions made via the SubscribeLocalEvent subscribers helper
        // (the inner builder passed to `Subscribes(builder => { ... })`), which is
        // a legitimate Initialize-time pattern reached through a lambda.
        var method = invocation.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (method is null)
            return;

        // Allow the widespread setup-helper naming conventions that upstream
        // partials use to chain work out of Initialize(): InitializeXxx
        // (SharedBuckleSystem.Buckle.cs), InitXxx (ExplosionSystem.Visuals.cs
        // InitVisuals, RadiationSystem.Blockers.cs InitRadBlocking),
        // SubscribeXxx (MobStateSystem.Subscribers.cs SubscribeEvents).
        var methodName = method.Identifier.ValueText;
        if (methodName.StartsWith("Init") || methodName.StartsWith("Subscribe"))
            return;

        // Verify this subscription resolves to an EntitySystem member, not
        // a lookalike on an unrelated type.
        var symbol = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol;
        if (symbol is not IMethodSymbol methodSymbol)
            return;

        if (!IsEntitySystemMember(methodSymbol.ContainingType))
            return;

        var containingType = context.ContainingSymbol?.ContainingType;
        if (containingType is null || !IsEntitySystemMember(containingType))
            return;

        context.ReportDiagnostic(Diagnostic.Create(Descriptor, invocation.GetLocation(), name));
    }

    private static string? GetInvokedName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            IdentifierNameSyntax id => id.Identifier.ValueText,
            GenericNameSyntax gn => gn.Identifier.ValueText,
            MemberAccessExpressionSyntax m => m.Name.Identifier.ValueText,
            _ => null,
        };
    }

    private static bool IsForkFile(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        return path!.Replace('\\', '/').Contains("/RussStation/")
            || path.EndsWith(".Honk.cs", StringComparison.Ordinal);
    }

    private static bool IsEntitySystemMember(INamedTypeSymbol? type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.Name == "EntitySystem" &&
                current.ContainingNamespace?.ToDisplayString() == "Robust.Shared.GameObjects")
            {
                return true;
            }
        }
        return false;
    }
}
