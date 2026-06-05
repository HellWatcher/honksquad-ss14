using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;

namespace Content.Analyzers.Honk.Tests;

using VerifyCS = CSharpAnalyzerTest<HonkDuplicateProtoIdConstantAnalyzer, DefaultVerifier>;

[TestFixture]
public sealed class HonkDuplicateProtoIdConstantAnalyzerTest
{
    private const string Stubs = """
        namespace Robust.Shared.Prototypes
        {
            public readonly struct ProtoId<T>
            {
                public ProtoId(string id) { }
                public static implicit operator ProtoId<T>(string s) => default;
            }
        }
        namespace Content.Shared.Stacks
        {
            public sealed class StackPrototype { }
        }
        """;

    private static Task Verify(
        (string FilePath, string Code)[] sources,
        params DiagnosticResult[] expected)
    {
        var test = new VerifyCS
        {
            TestState =
            {
                Sources = { Stubs },
            },
        };
        foreach (var (filePath, code) in sources)
            test.TestState.Sources.Add((filePath, code));
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Test]
    public async Task SameProtoIdLiteral_TwoForkFiles_BothReport()
    {
        const string fileA = """
            using Content.Shared.Stacks;
            using Robust.Shared.Prototypes;

            public sealed class IdCardAccountSystem
            {
                public static readonly ProtoId<StackPrototype> Credit = "Credit";
            }
            """;

        const string fileB = """
            using Content.Shared.Stacks;
            using Robust.Shared.Prototypes;

            public sealed class VendingPaymentSystem
            {
                public static readonly ProtoId<StackPrototype> Credit = "Credit";
            }
            """;

        const string pathA = "Content.Server/RussStation/Economy/IdCardAccountSystem.cs";
        const string pathB = "Content.Server/RussStation/Economy/VendingPaymentSystem.cs";

        await Verify(
            new[] { (pathA, fileA), (pathB, fileB) },
            new DiagnosticResult("HONK0026", DiagnosticSeverity.Info)
                .WithSpan(pathA, 6, 52, 6, 58)
                .WithArguments("StackPrototype", "Credit", "2"),
            new DiagnosticResult("HONK0026", DiagnosticSeverity.Info)
                .WithSpan(pathB, 6, 52, 6, 58)
                .WithArguments("StackPrototype", "Credit", "2"));
    }

    [Test]
    public async Task SingleDeclaration_DoesNotReport()
    {
        const string code = """
            using Content.Shared.Stacks;
            using Robust.Shared.Prototypes;

            public sealed class IdCardAccountSystem
            {
                public static readonly ProtoId<StackPrototype> Credit = "Credit";
            }
            """;

        await Verify(new[] { ("Content.Server/RussStation/Economy/IdCardAccountSystem.cs", code) });
    }

    [Test]
    public async Task DifferentLiterals_DoNotReport()
    {
        const string fileA = """
            using Content.Shared.Stacks;
            using Robust.Shared.Prototypes;

            public sealed class IdCardAccountSystem
            {
                public static readonly ProtoId<StackPrototype> Credit = "Credit";
            }
            """;

        const string fileB = """
            using Content.Shared.Stacks;
            using Robust.Shared.Prototypes;

            public sealed class VendingPaymentSystem
            {
                public static readonly ProtoId<StackPrototype> Plasma = "Plasma";
            }
            """;

        await Verify(new[]
        {
            ("Content.Server/RussStation/Economy/IdCardAccountSystem.cs", fileA),
            ("Content.Server/RussStation/Economy/VendingPaymentSystem.cs", fileB),
        });
    }

    [Test]
    public async Task OneForkOneUpstream_DoesNotReport()
    {
        const string forkFile = """
            using Content.Shared.Stacks;
            using Robust.Shared.Prototypes;

            public sealed class IdCardAccountSystem
            {
                public static readonly ProtoId<StackPrototype> Credit = "Credit";
            }
            """;

        const string upstreamFile = """
            using Content.Shared.Stacks;
            using Robust.Shared.Prototypes;

            public sealed class CargoSystem
            {
                public static readonly ProtoId<StackPrototype> Credit = "Credit";
            }
            """;

        await Verify(new[]
        {
            ("Content.Server/RussStation/Economy/IdCardAccountSystem.cs", forkFile),
            ("Content.Server/Cargo/Systems/CargoSystem.cs", upstreamFile),
        });
    }

    [Test]
    public async Task HonkPartialFiles_BothReport()
    {
        const string fileA = """
            using Content.Shared.Stacks;
            using Robust.Shared.Prototypes;

            public sealed class IdCardAccountSystem
            {
                public static readonly ProtoId<StackPrototype> CreditStack = "Credit";
            }
            """;

        const string fileB = """
            using Content.Shared.Stacks;
            using Robust.Shared.Prototypes;

            public sealed class VendingPaymentSystem
            {
                public static readonly ProtoId<StackPrototype> CreditStack = "Credit";
            }
            """;

        const string pathA = "Content.Shared/Economy/IdCardAccountSystem.Honk.cs";
        const string pathB = "Content.Shared/Economy/VendingPaymentSystem.Honk.cs";

        await Verify(
            new[] { (pathA, fileA), (pathB, fileB) },
            new DiagnosticResult("HONK0026", DiagnosticSeverity.Info)
                .WithSpan(pathA, 6, 52, 6, 63)
                .WithArguments("StackPrototype", "Credit", "2"),
            new DiagnosticResult("HONK0026", DiagnosticSeverity.Info)
                .WithSpan(pathB, 6, 52, 6, 63)
                .WithArguments("StackPrototype", "Credit", "2"));
    }
}
