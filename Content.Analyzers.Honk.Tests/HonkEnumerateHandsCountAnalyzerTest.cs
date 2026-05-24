using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;

namespace Content.Analyzers.Honk.Tests;

using VerifyCS = CSharpAnalyzerTest<HonkEnumerateHandsCountAnalyzer, DefaultVerifier>;

[TestFixture]
public sealed class HonkEnumerateHandsCountAnalyzerTest
{
    private const string Stubs = """
        using System.Collections.Generic;

        public class SharedHandsSystem
        {
            public IEnumerable<string> EnumerateHands(int uid) => System.Array.Empty<string>();
        }
        public sealed class HandsSystem : SharedHandsSystem { }
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
    public async Task EnumerateHandsCount_ForkFile_Reports()
    {
        const string code = """
            using System.Linq;

            public static class T
            {
                public static int Run(HandsSystem hands) => hands.EnumerateHands(0).Count();
            }
            """;

        await Verify(code, "Content.Shared/RussStation/Foo/FooSystem.cs",
            new DiagnosticResult("HONK0022", DiagnosticSeverity.Warning)
                .WithSpan("Content.Shared/RussStation/Foo/FooSystem.cs", 5, 73, 5, 78)
                .WithArguments("Count"));
    }

    [Test]
    public async Task EnumerateHandsAny_ForkFile_Reports()
    {
        const string code = """
            using System.Linq;

            public static class T
            {
                public static bool Run(HandsSystem hands) => hands.EnumerateHands(0).Any();
            }
            """;

        await Verify(code, "Content.Shared/RussStation/Foo/FooSystem.cs",
            new DiagnosticResult("HONK0022", DiagnosticSeverity.Warning)
                .WithSpan("Content.Shared/RussStation/Foo/FooSystem.cs", 5, 74, 5, 77)
                .WithArguments("Any"));
    }

    [Test]
    public async Task EnumerateHandsForeach_DoesNotReport()
    {
        const string code = """
            public static class T
            {
                public static void Run(HandsSystem hands)
                {
                    foreach (var h in hands.EnumerateHands(0)) { }
                }
            }
            """;

        await Verify(code, "Content.Shared/RussStation/Foo/FooSystem.cs");
    }

    [Test]
    public async Task UpstreamFile_DoesNotReport()
    {
        const string code = """
            using System.Linq;

            public static class T
            {
                public static int Run(HandsSystem hands) => hands.EnumerateHands(0).Count();
            }
            """;

        await Verify(code, "Content.Shared/Foo/FooSystem.cs");
    }

    [Test]
    public async Task UnrelatedEnumerableCount_DoesNotReport()
    {
        const string code = """
            using System.Collections.Generic;
            using System.Linq;

            public static class T
            {
                public static int Run() => new List<string> { "a", "b" }.Count();
            }
            """;

        await Verify(code, "Content.Shared/RussStation/Foo/FooSystem.cs");
    }
}
