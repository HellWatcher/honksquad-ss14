using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;

namespace Content.Analyzers.Honk.Tests;

using VerifyCS = CSharpAnalyzerTest<HonkSandboxConvertToInt32Analyzer, DefaultVerifier>;

[TestFixture]
public sealed class HonkSandboxConvertToInt32AnalyzerTest
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
    public async Task ConvertToInt32_Object_InSharedForkFile_Reports()
    {
        const string code = """
            public enum Mode { A, B }

            public static class T
            {
                public static int Run()
                {
                    Mode m = Mode.A;
                    return System.Convert.ToInt32((object)m);
                }
            }
            """;

        await Verify(code, "Content.Shared/RussStation/Foo/FooSystem.cs",
            new DiagnosticResult("HONK0020", DiagnosticSeverity.Warning)
                .WithSpan("Content.Shared/RussStation/Foo/FooSystem.cs", 8, 16, 8, 49));
    }

    [Test]
    public async Task ConvertToInt32_Object_InServer_DoesNotReport()
    {
        const string code = """
            public enum Mode { A, B }

            public static class T
            {
                public static int Run()
                {
                    Mode m = Mode.A;
                    return System.Convert.ToInt32((object)m);
                }
            }
            """;

        await Verify(code, "Content.Server/RussStation/Foo/FooSystem.cs");
    }

    [Test]
    public async Task ConvertToInt32_String_DoesNotReport()
    {
        const string code = """
            public static class T
            {
                public static int Run()
                {
                    return System.Convert.ToInt32("42");
                }
            }
            """;

        await Verify(code, "Content.Shared/RussStation/Foo/FooSystem.cs");
    }

    [Test]
    public async Task ConvertToInt32_Object_InUpstreamShared_DoesNotReport()
    {
        const string code = """
            public enum Mode { A, B }

            public static class T
            {
                public static int Run()
                {
                    Mode m = Mode.A;
                    return System.Convert.ToInt32((object)m);
                }
            }
            """;

        await Verify(code, "Content.Shared/Foo/FooSystem.cs");
    }

    [Test]
    public async Task ConvertToInt32_Object_InHonkPartialShared_Reports()
    {
        const string code = """
            public enum Mode { A, B }

            public static class T
            {
                public static int Run()
                {
                    Mode m = Mode.A;
                    return System.Convert.ToInt32((object)m);
                }
            }
            """;

        await Verify(code, "Content.Shared/Foo/FooSystem.Honk.cs",
            new DiagnosticResult("HONK0020", DiagnosticSeverity.Warning)
                .WithSpan("Content.Shared/Foo/FooSystem.Honk.cs", 8, 16, 8, 49));
    }
}
