using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;

namespace Content.Analyzers.Honk.Tests;

using VerifyCS = CSharpAnalyzerTest<HonkInterpolatedLogAnalyzer, DefaultVerifier>;

[TestFixture]
public sealed class HonkInterpolatedLogAnalyzerTest
{
    private const string Stubs = """
        namespace Honk.Logging
        {
            public static class Log
            {
                public static void Debug(string s) { }
                public static void Info(string s) { }
                public static void Warning(string s) { }
                public static void Error(string s) { }
                public static void Fatal(string s) { }
                public static void Verbose(string s) { }
            }

            public sealed class StubSawmill
            {
                public void Debug(string s) { }
                public void Info(string s) { }
                public void Warning(string s) { }
                public void Error(string s) { }
            }

            public static class Foo
            {
                public static void Warning(string s) { }
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
    public async Task InterpolatedLogWarning_ForkFile_Reports()
    {
        const string code = """
            using Honk.Logging;

            public sealed class Bar
            {
                public void Run(int y)
                {
                    Log.Warning($"x{y}");
                }
            }
            """;

        await Verify(code, "Content.Server/RussStation/Foo/Bar.cs",
            new DiagnosticResult("HONK0028", DiagnosticSeverity.Warning)
                .WithSpan("Content.Server/RussStation/Foo/Bar.cs", 7, 9, 7, 29)
                .WithArguments("Warning"));
    }

    [Test]
    public async Task PlainLiteralLog_DoesNotReport()
    {
        const string code = """
            using Honk.Logging;

            public sealed class Bar
            {
                public void Run()
                {
                    Log.Warning("plain literal");
                }
            }
            """;

        await Verify(code, "Content.Server/RussStation/Foo/Bar.cs");
    }

    [Test]
    public async Task SawmillFieldInterpolated_ForkFile_Reports()
    {
        const string code = """
            using Honk.Logging;

            public sealed class Bar
            {
                private readonly StubSawmill _sawmill = new();

                public void Run(int b)
                {
                    _sawmill.Debug($"a{b}");
                }
            }
            """;

        await Verify(code, "Content.Server/RussStation/Foo/Bar.cs",
            new DiagnosticResult("HONK0028", DiagnosticSeverity.Warning)
                .WithSpan("Content.Server/RussStation/Foo/Bar.cs", 9, 9, 9, 32)
                .WithArguments("Debug"));
    }

    [Test]
    public async Task StringConcatLog_ForkFile_Reports()
    {
        const string code = """
            using Honk.Logging;

            public sealed class Bar
            {
                public void Run(string b)
                {
                    Log.Error("a" + b);
                }
            }
            """;

        await Verify(code, "Content.Server/RussStation/Foo/Bar.cs",
            new DiagnosticResult("HONK0028", DiagnosticSeverity.Warning)
                .WithSpan("Content.Server/RussStation/Foo/Bar.cs", 7, 9, 7, 27)
                .WithArguments("Error"));
    }

    [Test]
    public async Task NonLoggerReceiver_DoesNotReport()
    {
        const string code = """
            using Honk.Logging;

            public sealed class Bar
            {
                public void Run(int y)
                {
                    Foo.Warning($"x{y}");
                }
            }
            """;

        await Verify(code, "Content.Server/RussStation/Foo/Bar.cs");
    }

    [Test]
    public async Task UpstreamFile_DoesNotReport()
    {
        const string code = """
            using Honk.Logging;

            public sealed class Bar
            {
                public void Run(int y)
                {
                    Log.Warning($"x{y}");
                }
            }
            """;

        await Verify(code, "Content.Server/Foo/Bar.cs");
    }

    [Test]
    public async Task HonkPartialFile_Reports()
    {
        const string code = """
            using Honk.Logging;

            public sealed class Bar
            {
                public void Run(int y)
                {
                    Log.Warning($"x{y}");
                }
            }
            """;

        await Verify(code, "Content.Server/Foo/Bar.Honk.cs",
            new DiagnosticResult("HONK0028", DiagnosticSeverity.Warning)
                .WithSpan("Content.Server/Foo/Bar.Honk.cs", 7, 9, 7, 29)
                .WithArguments("Warning"));
    }
}
