using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Content.Analyzers.Honk;

/// <summary>
/// HONK0002: a `*.Honk.cs` partial class exposing more than three public
/// mutation entry points (public property setters or `public void SetX()` /
/// `Reset*` methods) is effectively re-exporting the underlying component.
/// At that point, the fork should escalate to a HONK-marked
/// `[Access(typeof(ForkSystem))]` on the upstream component instead of
/// piling on partial helpers.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HonkPartialSurfaceAnalyzer : DiagnosticAnalyzer
{
    private const int Threshold = 3;

    private static readonly DiagnosticDescriptor Descriptor = new(
        id: "HONK0002",
        title: "Honk partial exposes too many public setters",
        messageFormat: "'{0}' exposes {1} public setters or Set*/Reset* methods in a .Honk.cs partial; escalate to a HONK-marked [Access(typeof(ForkSystem))] on the upstream type instead",
        category: "Honk.Access",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Partial-class setters are a scalpel for one or two fields the fork needs to mutate. Beyond three, they signal that a real [Access] friend is overdue, pushing back against a blob of re-exports growing over time.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ClassDeclaration);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var cls = (ClassDeclarationSyntax)context.Node;

        if (!IsHonkPartialFile(cls.SyntaxTree.FilePath))
            return;

        if (!cls.Modifiers.Any(SyntaxKind.PartialKeyword))
            return;

        var count = 0;
        foreach (var member in cls.Members)
        {
            if (IsPublicSetterSurface(member))
                count++;
        }

        if (count <= Threshold)
            return;

        context.ReportDiagnostic(Diagnostic.Create(Descriptor, cls.Identifier.GetLocation(), cls.Identifier.ValueText, count));
    }

    private static bool IsHonkPartialFile(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return false;
        return path!.Replace('\\', '/').EndsWith(".Honk.cs", StringComparison.Ordinal);
    }

    private static bool IsPublicSetterSurface(MemberDeclarationSyntax member)
    {
        if (!member.Modifiers.Any(SyntaxKind.PublicKeyword))
            return false;

        switch (member)
        {
            case PropertyDeclarationSyntax prop:
                if (prop.AccessorList is null)
                    return false;
                foreach (var accessor in prop.AccessorList.Accessors)
                {
                    if (!accessor.Keyword.IsKind(SyntaxKind.SetKeyword))
                        continue;
                    if (accessor.Modifiers.Any(SyntaxKind.PrivateKeyword)
                        || accessor.Modifiers.Any(SyntaxKind.InternalKeyword)
                        || accessor.Modifiers.Any(SyntaxKind.ProtectedKeyword))
                    {
                        return false;
                    }
                    return true;
                }
                return false;

            case MethodDeclarationSyntax method:
                var name = method.Identifier.ValueText;
                return name.StartsWith("Set", StringComparison.Ordinal)
                    || name.StartsWith("Reset", StringComparison.Ordinal);

            default:
                return false;
        }
    }
}
