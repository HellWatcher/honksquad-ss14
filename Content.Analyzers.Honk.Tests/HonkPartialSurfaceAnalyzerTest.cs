using System.Threading.Tasks;
using Content.Analyzers.Honk;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;

namespace Content.Analyzers.Honk.Tests;

using VerifyCS = CSharpAnalyzerTest<HonkPartialSurfaceAnalyzer, DefaultVerifier>;

[TestFixture]
public sealed class HonkPartialSurfaceAnalyzerTest
{
    private static Task Verify(string code, string filePath, params DiagnosticResult[] expected)
    {
        var test = new VerifyCS
        {
            TestState =
            {
                Sources =
                {
                    (filePath, code),
                },
            },
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Test]
    public async Task HonkPartial_FourSetMethods_Reports()
    {
        const string code = """
            namespace Foo;
            public abstract partial class SomeSystem
            {
                public void SetA() { }
                public void SetB() { }
                public void SetC() { }
                public void SetD() { }
            }
            """;

        await Verify(code, "Content.Shared/Foo/SomeSystem.Honk.cs",
            new DiagnosticResult("HONK0002", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithSpan("Content.Shared/Foo/SomeSystem.Honk.cs", 2, 31, 2, 41)
                .WithArguments("SomeSystem", 4));
    }

    [Test]
    public async Task HonkPartial_ThreeSetMethods_DoesNotReport()
    {
        const string code = """
            namespace Foo;
            public abstract partial class SomeSystem
            {
                public void SetA() { }
                public void SetB() { }
                public void SetC() { }
            }
            """;

        await Verify(code, "Content.Shared/Foo/SomeSystem.Honk.cs");
    }

    [Test]
    public async Task HonkPartial_MixedSettersAndMethods_Reports()
    {
        const string code = """
            namespace Foo;
            public partial class SomeComponent
            {
                public int A { get; set; }
                public int B { get; set; }
                public void SetC() { }
                public void ResetD() { }
            }
            """;

        await Verify(code, "Content.Shared/Foo/SomeComponent.Honk.cs",
            new DiagnosticResult("HONK0002", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithSpan("Content.Shared/Foo/SomeComponent.Honk.cs", 2, 22, 2, 35)
                .WithArguments("SomeComponent", 4));
    }

    [Test]
    public async Task NonHonkFile_ManySetMethods_DoesNotReport()
    {
        const string code = """
            namespace Foo;
            public partial class SomeSystem
            {
                public void SetA() { }
                public void SetB() { }
                public void SetC() { }
                public void SetD() { }
                public void SetE() { }
            }
            """;

        await Verify(code, "Content.Shared/Foo/SomeSystem.cs");
    }

    [Test]
    public async Task HonkPartial_PrivateSetter_DoesNotCount()
    {
        const string code = """
            namespace Foo;
            public partial class SomeComponent
            {
                public int A { get; private set; }
                public int B { get; private set; }
                public int C { get; private set; }
                public int D { get; private set; }
            }
            """;

        await Verify(code, "Content.Shared/Foo/SomeComponent.Honk.cs");
    }
}
