using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;

namespace Content.Analyzers.Honk.Tests;

using VerifyCS = CSharpAnalyzerTest<HonkAutoNetworkedDoAfterIdAnalyzer, DefaultVerifier>;

[TestFixture]
public sealed class HonkAutoNetworkedDoAfterIdAnalyzerTest
{
    private const string Stubs = """
        namespace Robust.Shared.Analyzers
        {
            [System.AttributeUsage(System.AttributeTargets.Field | System.AttributeTargets.Property)]
            public sealed class AutoNetworkedFieldAttribute : System.Attribute { }
        }
        namespace Content.Shared.DoAfter
        {
            public readonly struct DoAfterId { }
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
    public async Task DoAfterIdField_WithAttribute_ForkFile_Reports()
    {
        const string code = """
            using Content.Shared.DoAfter;
            using Robust.Shared.Analyzers;

            public sealed class FooComponent
            {
                [AutoNetworkedField]
                public DoAfterId? Active;
            }
            """;

        await Verify(code, "Content.Shared/RussStation/Foo/FooComponent.cs",
            new DiagnosticResult("HONK0019", DiagnosticSeverity.Warning)
                .WithSpan("Content.Shared/RussStation/Foo/FooComponent.cs", 7, 23, 7, 29)
                .WithArguments("Active"));
    }

    [Test]
    public async Task DoAfterIdProperty_WithAttribute_ForkFile_Reports()
    {
        const string code = """
            using Content.Shared.DoAfter;
            using Robust.Shared.Analyzers;

            public sealed class FooComponent
            {
                [AutoNetworkedField]
                public DoAfterId Active { get; set; }
            }
            """;

        await Verify(code, "Content.Shared/RussStation/Foo/FooComponent.cs",
            new DiagnosticResult("HONK0019", DiagnosticSeverity.Warning)
                .WithSpan("Content.Shared/RussStation/Foo/FooComponent.cs", 7, 22, 7, 28)
                .WithArguments("Active"));
    }

    [Test]
    public async Task AttributeOnUnrelatedType_DoesNotReport()
    {
        const string code = """
            using Robust.Shared.Analyzers;

            public sealed class FooComponent
            {
                [AutoNetworkedField]
                public int Count;
            }
            """;

        await Verify(code, "Content.Shared/RussStation/Foo/FooComponent.cs");
    }

    [Test]
    public async Task DoAfterIdWithoutAttribute_DoesNotReport()
    {
        const string code = """
            using Content.Shared.DoAfter;

            public sealed class FooComponent
            {
                public DoAfterId? Active;
            }
            """;

        await Verify(code, "Content.Shared/RussStation/Foo/FooComponent.cs");
    }

    [Test]
    public async Task UpstreamFile_DoesNotReport()
    {
        const string code = """
            using Content.Shared.DoAfter;
            using Robust.Shared.Analyzers;

            public sealed class FooComponent
            {
                [AutoNetworkedField]
                public DoAfterId? Active;
            }
            """;

        await Verify(code, "Content.Shared/Foo/FooComponent.cs");
    }

    [Test]
    public async Task HonkPartialFile_Reports()
    {
        const string code = """
            using Content.Shared.DoAfter;
            using Robust.Shared.Analyzers;

            public sealed class FooComponent
            {
                [AutoNetworkedField]
                public DoAfterId? Active;
            }
            """;

        await Verify(code, "Content.Shared/Foo/FooComponent.Honk.cs",
            new DiagnosticResult("HONK0019", DiagnosticSeverity.Warning)
                .WithSpan("Content.Shared/Foo/FooComponent.Honk.cs", 7, 23, 7, 29)
                .WithArguments("Active"));
    }
}
