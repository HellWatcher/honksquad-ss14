using System.Threading.Tasks;
using Content.Analyzers.Honk;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;

namespace Content.Analyzers.Honk.Tests;

using VerifyCS = CSharpAnalyzerTest<HonkNetworkedComponentAccessAnalyzer, DefaultVerifier>;

[TestFixture]
public sealed class HonkNetworkedComponentAccessAnalyzerTest
{
    private const string Stubs = """
        namespace Robust.Shared.GameObjects
        {
            public abstract class Component { }

            [System.AttributeUsage(System.AttributeTargets.Class)]
            public sealed class RegisterComponentAttribute : System.Attribute { }

            [System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = true)]
            public sealed class AccessAttribute : System.Attribute
            {
                public AccessAttribute(System.Type type) { }
            }

            [System.AttributeUsage(System.AttributeTargets.Class)]
            public sealed class AutoGenerateComponentStateAttribute : System.Attribute { }

            [System.AttributeUsage(System.AttributeTargets.Field | System.AttributeTargets.Property)]
            public sealed class AutoNetworkedFieldAttribute : System.Attribute { }
        }
        namespace Robust.Shared.GameStates
        {
            [System.AttributeUsage(System.AttributeTargets.Class)]
            public sealed class NetworkedComponentAttribute : System.Attribute { }
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
    public async Task NetworkedComponent_WithoutAccess_Reports()
    {
        const string code = """
            using Robust.Shared.GameObjects;
            using Robust.Shared.GameStates;

            [RegisterComponent, NetworkedComponent]
            public sealed partial class FooComponent : Component { }
            """;

        await Verify(code, ForkPath,
            new DiagnosticResult("HONK0018", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                .WithSpan(ForkPath, 5, 29, 5, 41)
                .WithArguments("FooComponent"));
    }

    [Test]
    public async Task NetworkedComponent_WithAccess_DoesNotReport()
    {
        const string code = """
            using Robust.Shared.GameObjects;
            using Robust.Shared.GameStates;

            public sealed class FooSystem { }

            [RegisterComponent, NetworkedComponent, Access(typeof(FooSystem))]
            public sealed partial class FooComponent : Component { }
            """;

        await Verify(code, ForkPath);
    }

    [Test]
    public async Task AutoGenerateComponentState_WithoutAccess_Reports()
    {
        const string code = """
            using Robust.Shared.GameObjects;

            [RegisterComponent, AutoGenerateComponentState]
            public sealed partial class FooComponent : Component { }
            """;

        await Verify(code, ForkPath,
            new DiagnosticResult("HONK0018", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                .WithSpan(ForkPath, 4, 29, 4, 41)
                .WithArguments("FooComponent"));
    }

    [Test]
    public async Task AutoNetworkedField_WithoutAccess_Reports()
    {
        const string code = """
            using Robust.Shared.GameObjects;

            [RegisterComponent]
            public sealed partial class FooComponent : Component
            {
                [AutoNetworkedField]
                public int Value;
            }
            """;

        await Verify(code, ForkPath,
            new DiagnosticResult("HONK0018", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                .WithSpan(ForkPath, 4, 29, 4, 41)
                .WithArguments("FooComponent"));
    }

    [Test]
    public async Task NonNetworkedComponent_DoesNotReport()
    {
        const string code = """
            using Robust.Shared.GameObjects;

            [RegisterComponent]
            public sealed partial class TagComponent : Component { }
            """;

        await Verify(code, ForkPath);
    }

    [Test]
    public async Task UpstreamFile_NetworkedWithoutAccess_DoesNotReport()
    {
        const string code = """
            using Robust.Shared.GameObjects;
            using Robust.Shared.GameStates;

            [RegisterComponent, NetworkedComponent]
            public sealed partial class UpstreamComponent : Component { }
            """;

        await Verify(code, "Content.Shared/Body/UpstreamComponent.cs");
    }

    [Test]
    public async Task HonkPartial_NetworkedWithoutAccess_Reports()
    {
        const string code = """
            using Robust.Shared.GameObjects;
            using Robust.Shared.GameStates;

            [RegisterComponent, NetworkedComponent]
            public sealed partial class FooComponent : Component { }
            """;

        const string path = "Content.Shared/Body/FooComponent.Honk.cs";
        await Verify(code, path,
            new DiagnosticResult("HONK0018", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                .WithSpan(path, 5, 29, 5, 41)
                .WithArguments("FooComponent"));
    }
}
