using System.Threading.Tasks;
using Content.Analyzers.Honk;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;

namespace Content.Analyzers.Honk.Tests;

using VerifyBalance = CSharpAnalyzerTest<HonkMarkerBalanceAnalyzer, DefaultVerifier>;

[TestFixture]
public sealed class HonkMarkerBalanceAnalyzerTest
{
    private static Task Verify(string code, params DiagnosticResult[] expected)
    {
        var test = new VerifyBalance
        {
            TestState =
            {
                Sources = { code },
            },
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Test]
    public async Task Balanced_Pair_DoesNotReport()
    {
        const string code = """
            // HONK START
            public sealed class Foo { }
            // HONK END
            """;
        await Verify(code);
    }

    [Test]
    public async Task OrphanStart_Reports()
    {
        const string code = """
            // HONK START
            public sealed class Foo { }
            """;
        await Verify(code,
            new DiagnosticResult("HONK0004", DiagnosticSeverity.Error)
                .WithSpan("/0/Test0.cs", 1, 1, 1, 14)
                .WithArguments("HONK START", "HONK END"));
    }

    [Test]
    public async Task OrphanEnd_Reports()
    {
        const string code = """
            public sealed class Foo { }
            // HONK END
            """;
        await Verify(code,
            new DiagnosticResult("HONK0004", DiagnosticSeverity.Error)
                .WithSpan("/0/Test0.cs", 2, 1, 2, 12)
                .WithArguments("HONK END", "HONK START"));
    }

    [Test]
    public async Task TwoBalancedBlocks_DoNotReport()
    {
        const string code = """
            // HONK START
            public sealed class Foo { }
            // HONK END

            // HONK START
            public sealed class Bar { }
            // HONK END
            """;
        await Verify(code);
    }

    [Test]
    public async Task NestedStart_ReportsFirstAsOrphan()
    {
        const string code = """
            // HONK START
            public sealed class Foo { }
            // HONK START
            public sealed class Bar { }
            // HONK END
            """;
        await Verify(code,
            new DiagnosticResult("HONK0004", DiagnosticSeverity.Error)
                .WithSpan("/0/Test0.cs", 1, 1, 1, 14)
                .WithArguments("HONK START", "HONK END"));
    }

    [Test]
    public async Task UnspacedMarker_Balanced_DoesNotReport()
    {
        const string code = """
            //HONK START
            public sealed class Foo { }
            //HONK END
            """;
        await Verify(code);
    }
}
