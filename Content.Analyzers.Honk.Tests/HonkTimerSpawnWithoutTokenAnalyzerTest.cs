using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;

namespace Content.Analyzers.Honk.Tests;

using VerifyCS = CSharpAnalyzerTest<HonkTimerSpawnWithoutTokenAnalyzer, DefaultVerifier>;

[TestFixture]
public sealed class HonkTimerSpawnWithoutTokenAnalyzerTest
{
    private const string Stubs = """
        namespace System.Threading
        {
            public struct CancellationToken { }
        }
        namespace Robust.Shared.Timing
        {
            public static class Timer
            {
                public static void Spawn(int ms, System.Action onFired) { }
                public static void Spawn(int ms, System.Action onFired, System.Threading.CancellationToken ct) { }
            }
        }
        """;

    private static Task Verify(string code, string filePath, params DiagnosticResult[] expected)
    {
        var test = new VerifyCS
        {
            TestState =
            {
                Sources = { Stubs, (filePath, code) },
            },
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Test]
    public async Task TimerSpawnWithoutToken_ForkFile_Reports()
    {
        const string code = """
            using Robust.Shared.Timing;

            public sealed class FooSystem
            {
                public void Go()
                {
                    Timer.Spawn(1000, () => {});
                }
            }
            """;

        await Verify(code, "Content.Client/RussStation/Foo/FooSystem.cs",
            new DiagnosticResult("HONK0031", DiagnosticSeverity.Warning)
                .WithSpan("Content.Client/RussStation/Foo/FooSystem.cs", 7, 9, 7, 36));
    }

    [Test]
    public async Task TimerSpawnWithToken_DoesNotReport()
    {
        const string code = """
            using Robust.Shared.Timing;
            using System.Threading;

            public sealed class FooSystem
            {
                public void Go(CancellationToken token)
                {
                    Timer.Spawn(1000, () => {}, token);
                }
            }
            """;

        await Verify(code, "Content.Client/RussStation/Foo/FooSystem.cs");
    }

    [Test]
    public async Task UnrelatedSpawn_DoesNotReport()
    {
        const string code = """
            public static class Foo
            {
                public static void Spawn(int ms) { }
            }

            public sealed class FooSystem
            {
                public void Go()
                {
                    Foo.Spawn(1000);
                }
            }
            """;

        await Verify(code, "Content.Client/RussStation/Foo/FooSystem.cs");
    }

    [Test]
    public async Task UpstreamFile_DoesNotReport()
    {
        const string code = """
            using Robust.Shared.Timing;

            public sealed class FooSystem
            {
                public void Go()
                {
                    Timer.Spawn(1000, () => {});
                }
            }
            """;

        await Verify(code, "Content.Client/Foo/FooSystem.cs");
    }

    [Test]
    public async Task HonkPartialFile_Reports()
    {
        const string code = """
            using Robust.Shared.Timing;

            public sealed class FooSystem
            {
                public void Go()
                {
                    Timer.Spawn(1000, () => {});
                }
            }
            """;

        await Verify(code, "Content.Client/Foo/FooSystem.Honk.cs",
            new DiagnosticResult("HONK0031", DiagnosticSeverity.Warning)
                .WithSpan("Content.Client/Foo/FooSystem.Honk.cs", 7, 9, 7, 36));
    }
}
