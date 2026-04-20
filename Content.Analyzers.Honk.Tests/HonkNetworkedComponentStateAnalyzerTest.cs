using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;

namespace Content.Analyzers.Honk.Tests;

using VerifyCS = CSharpAnalyzerTest<HonkNetworkedComponentStateAnalyzer, DefaultVerifier>;

[TestFixture]
public sealed class HonkNetworkedComponentStateAnalyzerTest
{
    private const string Stubs = """
        namespace Robust.Shared.GameStates
        {
            [System.AttributeUsage(System.AttributeTargets.Class, Inherited = true)]
            public sealed class NetworkedComponentAttribute : System.Attribute { }
            [System.AttributeUsage(System.AttributeTargets.Class)]
            public sealed class AutoGenerateComponentStateAttribute : System.Attribute
            {
                public AutoGenerateComponentStateAttribute(bool raiseAfterAutoHandleState = false) { }
            }
        }
        namespace Robust.Shared.Serialization.Manager.Attributes
        {
            [System.AttributeUsage(System.AttributeTargets.Field | System.AttributeTargets.Property)]
            public sealed class DataFieldAttribute : System.Attribute { }
        }
        namespace Robust.Shared.Analyzers
        {
            [System.AttributeUsage(System.AttributeTargets.Field | System.AttributeTargets.Property)]
            public sealed class AutoNetworkedFieldAttribute : System.Attribute { }
        }
        namespace Robust.Shared.GameObjects
        {
            public interface IComponent { }
            public abstract class Component : IComponent { }
        }
        """;

    private const string ForkPath = "Content.Shared/RussStation/ForkComp.cs";
    private const string UpstreamPath = "Content.Shared/Upstream/UpstreamComp.cs";

    private static Task Verify(string code, string filePath, params DiagnosticResult[] expected)
    {
        var test = new VerifyCS { TestState = { Sources = { Stubs, (filePath, code) } } };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Test]
    public async Task NetworkedWithDataFieldButNoState_Reports()
    {
        const string code = """
            using Robust.Shared.GameObjects;
            using Robust.Shared.GameStates;
            using Robust.Shared.Serialization.Manager.Attributes;

            [NetworkedComponent]
            public sealed partial class Leaky : Component
            {
                [DataField]
                public int Value;
            }
            """;

        await Verify(code, ForkPath,
            new DiagnosticResult("HONK0012", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithSpan(ForkPath, 6, 29, 6, 34)
                .WithArguments("Leaky"));
    }

    [Test]
    public async Task NetworkedWithAutoState_DoesNotReport()
    {
        const string code = """
            using Robust.Shared.GameObjects;
            using Robust.Shared.GameStates;
            using Robust.Shared.Serialization.Manager.Attributes;

            [NetworkedComponent, AutoGenerateComponentState]
            public sealed partial class Fine : Component
            {
                [DataField]
                public int Value;
            }
            """;

        await Verify(code, ForkPath);
    }

    [Test]
    public async Task NetworkedWithManualState_DoesNotReport()
    {
        const string code = """
            using Robust.Shared.GameObjects;
            using Robust.Shared.GameStates;
            using Robust.Shared.Serialization.Manager.Attributes;

            [NetworkedComponent]
            public sealed partial class Manual : Component
            {
                [DataField]
                public int Value;

                public void GetComponentState() { }
                public void HandleComponentState() { }
            }
            """;

        await Verify(code, ForkPath);
    }

    [Test]
    public async Task NonNetworkedWithDataField_DoesNotReport()
    {
        const string code = """
            using Robust.Shared.GameObjects;
            using Robust.Shared.Serialization.Manager.Attributes;

            public sealed partial class Plain : Component
            {
                [DataField]
                public int Value;
            }
            """;

        await Verify(code, ForkPath);
    }

    [Test]
    public async Task NetworkedWithoutDataFields_DoesNotReport()
    {
        const string code = """
            using Robust.Shared.GameObjects;
            using Robust.Shared.GameStates;

            [NetworkedComponent]
            public sealed partial class Flag : Component { }
            """;

        await Verify(code, ForkPath);
    }

    [Test]
    public async Task NetworkedWithDataFieldButNoState_InUpstreamFile_DoesNotReport()
    {
        const string code = """
            using Robust.Shared.GameObjects;
            using Robust.Shared.GameStates;
            using Robust.Shared.Serialization.Manager.Attributes;

            [NetworkedComponent]
            public sealed partial class UpstreamLeaky : Component
            {
                [DataField]
                public int Value;
            }
            """;

        await Verify(code, UpstreamPath);
    }
}
