using System.Threading.Tasks;
using Content.Analyzers.Honk;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;

namespace Content.Analyzers.Honk.Tests;

using VerifyCS = CSharpAnalyzerTest<HonkComponentRegistrationAnalyzer, DefaultVerifier>;

[TestFixture]
public sealed class HonkComponentRegistrationAnalyzerTest
{
    private const string Stubs = """
        namespace Robust.Shared.GameObjects
        {
            public abstract class Component { }

            [System.AttributeUsage(System.AttributeTargets.Class)]
            public sealed class RegisterComponentAttribute : System.Attribute { }
        }
        namespace Robust.Shared.Analyzers
        {
            [System.AttributeUsage(System.AttributeTargets.Class)]
            public sealed class VirtualAttribute : System.Attribute { }
        }
        """;

    private const string ForkPath = "Content.Shared/RussStation/SomeForkComponent.cs";

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
    public async Task ConcreteComponent_WithoutAttribute_Reports()
    {
        const string code = """
            using Robust.Shared.GameObjects;

            public sealed class FooComponent : Component { }
            """;

        await Verify(code, ForkPath,
            new DiagnosticResult("HONK0008", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithSpan(ForkPath, 3, 21, 3, 33)
                .WithArguments("FooComponent"));
    }

    [Test]
    public async Task ConcreteComponent_WithAttribute_DoesNotReport()
    {
        const string code = """
            using Robust.Shared.GameObjects;

            [RegisterComponent]
            public sealed class FooComponent : Component { }
            """;

        await Verify(code, ForkPath);
    }

    [Test]
    public async Task AbstractComponent_WithoutAttribute_DoesNotReport()
    {
        const string code = """
            using Robust.Shared.GameObjects;

            public abstract class BaseThingComponent : Component { }
            """;

        await Verify(code, ForkPath);
    }

    [Test]
    public async Task VirtualComponent_WithoutRegisterAttribute_DoesNotReport()
    {
        const string code = """
            using Robust.Shared.GameObjects;
            using Robust.Shared.Analyzers;

            [Virtual]
            public class SharedFooComponent : Component { }
            """;

        await Verify(code, ForkPath);
    }

    [Test]
    public async Task NonComponentClass_WithoutAttribute_DoesNotReport()
    {
        const string code = """
            public sealed class SomeHelper { }
            """;

        await Verify(code, ForkPath);
    }

    [Test]
    public async Task TransitivelyDerivedComponent_WithoutAttribute_Reports()
    {
        const string code = """
            using Robust.Shared.GameObjects;

            public abstract class BaseThingComponent : Component { }

            public sealed class DerivedThingComponent : BaseThingComponent { }
            """;

        await Verify(code, ForkPath,
            new DiagnosticResult("HONK0008", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithSpan(ForkPath, 5, 21, 5, 42)
                .WithArguments("DerivedThingComponent"));
    }

    [Test]
    public async Task TransitivelyDerivedComponent_WithAttribute_DoesNotReport()
    {
        const string code = """
            using Robust.Shared.GameObjects;

            public abstract class BaseThingComponent : Component { }

            [RegisterComponent]
            public sealed class DerivedThingComponent : BaseThingComponent { }
            """;

        await Verify(code, ForkPath);
    }

    [Test]
    public async Task UpstreamFile_WithoutAttribute_DoesNotReport()
    {
        const string code = """
            using Robust.Shared.GameObjects;

            public sealed class SharedDisposalRouterComponent : Component { }
            """;

        await Verify(code, "Content.Shared/Disposal/Mailing/SharedDisposalRouterComponent.cs");
    }

    [Test]
    public async Task HonkPartial_WithoutAttribute_Reports()
    {
        const string code = """
            using Robust.Shared.GameObjects;

            public sealed partial class FooComponent : Component { }
            """;

        const string path = "Content.Shared/Body/Components/FooComponent.Honk.cs";
        await Verify(code, path,
            new DiagnosticResult("HONK0008", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithSpan(path, 3, 29, 3, 41)
                .WithArguments("FooComponent"));
    }
}
