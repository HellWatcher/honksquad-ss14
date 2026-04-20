using System.Threading.Tasks;
using Content.Analyzers.Honk;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;

namespace Content.Analyzers.Honk.Tests;

using VerifyCS = CSharpAnalyzerTest<HonkAccessReadWriteAnalyzer, DefaultVerifier>;

[TestFixture]
public sealed class HonkAccessReadWriteAnalyzerTest
{
    private const string Stubs = """
        namespace Robust.Shared.Analyzers
        {
            public enum AccessPermissions { None, Read, ReadWrite }
            [System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = true)]
            public sealed class AccessAttribute : System.Attribute
            {
                public AccessAttribute() { }
                public AccessAttribute(System.Type type) { }
                public AccessPermissions Other { get; set; }
            }
        }
        """;

    private static Task Verify(string code, params DiagnosticResult[] expected)
    {
        var test = new VerifyCS
        {
            TestState =
            {
                Sources = { Stubs, code },
            },
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Test]
    public async Task ReadWrite_InsideHonkBlock_Reports()
    {
        const string code = """
            using Robust.Shared.Analyzers;

            // HONK START
            [Access(Other = AccessPermissions.ReadWrite)]
            // HONK END
            public sealed class Foo { }
            """;

        await Verify(code,
            new DiagnosticResult("HONK0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                .WithSpan("/0/Test1.cs", 4, 2, 4, 45));
    }

    [Test]
    public async Task ReadWrite_OutsideHonkBlock_DoesNotReport()
    {
        const string code = """
            using Robust.Shared.Analyzers;

            [Access(Other = AccessPermissions.ReadWrite)]
            public sealed class Foo { }
            """;

        await Verify(code);
    }

    [Test]
    public async Task ReadOnly_InsideHonkBlock_DoesNotReport()
    {
        const string code = """
            using Robust.Shared.Analyzers;

            // HONK START
            [Access(Other = AccessPermissions.Read)]
            // HONK END
            public sealed class Foo { }
            """;

        await Verify(code);
    }

    [Test]
    public async Task ReadWrite_WithUnspacedMarker_Reports()
    {
        const string code = """
            using Robust.Shared.Analyzers;

            //HONK START
            [Access(Other = AccessPermissions.ReadWrite)]
            //HONK END
            public sealed class Foo { }
            """;

        await Verify(code,
            new DiagnosticResult("HONK0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                .WithSpan("/0/Test1.cs", 4, 2, 4, 45));
    }

    [Test]
    public async Task ReadWrite_WithImportedEnum_InsideHonkBlock_Reports()
    {
        const string code = """
            using Robust.Shared.Analyzers;
            using static Robust.Shared.Analyzers.AccessPermissions;

            // HONK START
            [Access(Other = ReadWrite)]
            // HONK END
            public sealed class Foo { }
            """;

        await Verify(code,
            new DiagnosticResult("HONK0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                .WithSpan("/0/Test1.cs", 5, 2, 5, 27));
    }
}
