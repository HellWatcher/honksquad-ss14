using System.Threading.Tasks;
using Content.Analyzers.Honk;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;

namespace Content.Analyzers.Honk.Tests;

using VerifyCS = CSharpAnalyzerTest<HonkOpaqueLocKeyAnalyzer, DefaultVerifier>;

[TestFixture]
public sealed class HonkOpaqueLocKeyAnalyzerTest
{
    private const string Stubs = """
        public static class Loc
        {
            public static string GetString(string s) => s;
            public static string GetString(string s, params (string, object)[] args) => s;
        }
        """;

    private const string ForkPath = "Content.Shared/RussStation/Foo/FooSystem.cs";
    private const string UpstreamPath = "Content.Shared/Foo/FooSystem.cs";
    private const string HonkPath = "Content.Shared/Foo/FooSystem.Honk.cs";

    private static Task Verify(string code, string filePath, params DiagnosticResult[] expected)
    {
        var test = new VerifyCS
        {
            TestState =
            {
                Sources =
                {
                    Stubs,
                    (filePath, code),
                },
            },
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Test]
    public async Task LeadingHole_ForkFile_Reports()
    {
        const string code = """
            public sealed class FooSystem
            {
                public string Go(string key, object target)
                {
                    return Loc.GetString($"{key}-puller", ("target", target));
                }
            }
            """;

        await Verify(code, ForkPath,
            new DiagnosticResult("HONK0029", DiagnosticSeverity.Warning)
                .WithSpan(ForkPath, 5, 30, 5, 45));
    }

    [Test]
    public async Task LiteralPrefix_DoesNotReport()
    {
        const string code = """
            public sealed class FooSystem
            {
                public string Go(int tier)
                {
                    return Loc.GetString($"wound-{tier}");
                }
            }
            """;

        await Verify(code, ForkPath);
    }

    [Test]
    public async Task PlainStringLiteral_DoesNotReport()
    {
        const string code = """
            public sealed class FooSystem
            {
                public string Go()
                {
                    return Loc.GetString("plain");
                }
            }
            """;

        await Verify(code, ForkPath);
    }

    [Test]
    public async Task VariableKey_DoesNotReport()
    {
        const string code = """
            public sealed class FooSystem
            {
                public string Go(string someVariable)
                {
                    return Loc.GetString(someVariable);
                }
            }
            """;

        await Verify(code, ForkPath);
    }

    [Test]
    public async Task LeadingHole_UpstreamFile_DoesNotReport()
    {
        const string code = """
            public sealed class FooSystem
            {
                public string Go(string key, object target)
                {
                    return Loc.GetString($"{key}-puller", ("target", target));
                }
            }
            """;

        await Verify(code, UpstreamPath);
    }

    [Test]
    public async Task LeadingHole_HonkPartial_Reports()
    {
        const string code = """
            public sealed class FooSystem
            {
                public string Go(string key, object target)
                {
                    return Loc.GetString($"{key}-puller", ("target", target));
                }
            }
            """;

        await Verify(code, HonkPath,
            new DiagnosticResult("HONK0029", DiagnosticSeverity.Warning)
                .WithSpan(HonkPath, 5, 30, 5, 45));
    }
}
