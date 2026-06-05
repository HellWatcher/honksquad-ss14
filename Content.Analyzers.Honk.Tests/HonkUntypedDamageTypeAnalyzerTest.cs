using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;

namespace Content.Analyzers.Honk.Tests;

using VerifyCS = CSharpAnalyzerTest<HonkUntypedDamageTypeAnalyzer, DefaultVerifier>;

[TestFixture]
public sealed class HonkUntypedDamageTypeAnalyzerTest
{
    private static Task Verify(string code, string filePath, params DiagnosticResult[] expected)
    {
        var test = new VerifyCS
        {
            TestState =
            {
                Sources = { (filePath, code) },
            },
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Test]
    public async Task EqualityComparison_ForkFile_Reports()
    {
        const string code = """
            public sealed class Foo
            {
                public bool Check(string s)
                {
                    return s == "Slash";
                }
            }
            """;

        await Verify(code, "Content.Shared/RussStation/Wounds/Foo.cs",
            new DiagnosticResult("HONK0024", DiagnosticSeverity.Warning)
                .WithSpan("Content.Shared/RussStation/Wounds/Foo.cs", 5, 21, 5, 28)
                .WithArguments("Slash"));
    }

    [Test]
    public async Task DictionaryInitializer_ForkFile_Reports()
    {
        const string code = """
            using System.Collections.Generic;

            public sealed class Foo
            {
                private static readonly HashSet<string> Set = new()
                {
                    "Blunt",
                    "x",
                };
            }
            """;

        await Verify(code, "Content.Shared/RussStation/Wounds/Foo.cs",
            new DiagnosticResult("HONK0024", DiagnosticSeverity.Warning)
                .WithSpan("Content.Shared/RussStation/Wounds/Foo.cs", 7, 9, 7, 16)
                .WithArguments("Blunt"));
    }

    [Test]
    public async Task PlainNonContextString_DoesNotReport()
    {
        const string code = """
            public sealed class Foo
            {
                public string Label = "Slash";
            }
            """;

        await Verify(code, "Content.Shared/RussStation/Wounds/Foo.cs");
    }

    [Test]
    public async Task EqualityComparison_UpstreamFile_DoesNotReport()
    {
        const string code = """
            public sealed class Foo
            {
                public bool Check(string s)
                {
                    return s == "Slash";
                }
            }
            """;

        await Verify(code, "Content.Shared/Wounds/Foo.cs");
    }

    [Test]
    public async Task EqualityComparison_HonkPartialFile_Reports()
    {
        const string code = """
            public sealed class Foo
            {
                public bool Check(string s)
                {
                    return s == "Slash";
                }
            }
            """;

        await Verify(code, "Content.Shared/Wounds/Foo.Honk.cs",
            new DiagnosticResult("HONK0024", DiagnosticSeverity.Warning)
                .WithSpan("Content.Shared/Wounds/Foo.Honk.cs", 5, 21, 5, 28)
                .WithArguments("Slash"));
    }
}
