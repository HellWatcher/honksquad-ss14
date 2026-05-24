using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;

namespace Content.Analyzers.Honk.Tests;

using VerifyCS = CSharpAnalyzerTest<HonkNetworkedComponentNamespaceAnalyzer, DefaultVerifier>;

[TestFixture]
public sealed class HonkNetworkedComponentNamespaceAnalyzerTest
{
    private const string Stubs = """
        namespace Robust.Shared.GameStates
        {
            [System.AttributeUsage(System.AttributeTargets.Class)]
            public sealed class NetworkedComponentAttribute : System.Attribute { }
        }
        namespace Robust.Shared.GameObjects
        {
            // Re-exported so the typo resolves without the correct using.
            public sealed class NetworkedComponentAttribute : System.Attribute { }
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
    public async Task NetworkedComponent_MissingGameStatesUsing_Reports()
    {
        const string code = """
            using Robust.Shared.GameObjects;

            [NetworkedComponent]
            public sealed partial class FooComponent { }
            """;

        await Verify(code, "Content.Shared/RussStation/Foo/FooComponent.cs",
            new DiagnosticResult("HONK0023", DiagnosticSeverity.Info)
                .WithSpan("Content.Shared/RussStation/Foo/FooComponent.cs", 3, 2, 3, 20)
                .WithArguments("FooComponent"));
    }

    [Test]
    public async Task NetworkedComponent_WithGameStatesUsing_DoesNotReport()
    {
        const string code = """
            using Robust.Shared.GameStates;

            [NetworkedComponent]
            public sealed partial class FooComponent { }
            """;

        await Verify(code, "Content.Shared/RussStation/Foo/FooComponent.cs");
    }

    [Test]
    public async Task NoNetworkedComponentAttribute_DoesNotReport()
    {
        const string code = """
            public sealed partial class FooComponent { }
            """;

        await Verify(code, "Content.Shared/RussStation/Foo/FooComponent.cs");
    }

    [Test]
    public async Task UpstreamFile_DoesNotReport()
    {
        const string code = """
            using Robust.Shared.GameObjects;

            [NetworkedComponent]
            public sealed partial class FooComponent { }
            """;

        await Verify(code, "Content.Shared/Foo/FooComponent.cs");
    }

    [Test]
    public async Task UsingInsideNamespace_DoesNotReport()
    {
        const string code = """
            namespace Content.Shared.RussStation.Foo
            {
                using Robust.Shared.GameStates;

                [NetworkedComponent]
                public sealed partial class FooComponent { }
            }
            """;

        await Verify(code, "Content.Shared/RussStation/Foo/FooComponent.cs");
    }
}
