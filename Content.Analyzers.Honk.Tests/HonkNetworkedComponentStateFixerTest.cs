using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;

namespace Content.Analyzers.Honk.Tests;

using VerifyCS = CSharpCodeFixTest<HonkNetworkedComponentStateAnalyzer, HonkNetworkedComponentStateFixer, DefaultVerifier>;

[TestFixture]
public sealed class HonkNetworkedComponentStateFixerTest
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
        namespace Robust.Shared.GameObjects
        {
            public interface IComponent { }
            public abstract class Component : IComponent { }
        }
        """;

    private const string ForkPath = "Content.Shared/RussStation/ForkComp.cs";

    private static Task Verify(string before, string after, params DiagnosticResult[] expected)
    {
        var test = new VerifyCS
        {
            TestState = { Sources = { Stubs, (ForkPath, before) } },
            FixedState = { Sources = { Stubs, (ForkPath, after) } },
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Test]
    public async Task AddsAttributeAndPartialAndUsing_WhenAllMissing()
    {
        const string before = """
            using Robust.Shared.GameObjects;
            using Robust.Shared.GameStates;
            using Robust.Shared.Serialization.Manager.Attributes;

            [NetworkedComponent]
            public sealed class Leaky : Component
            {
                [DataField]
                public int Value;
            }
            """;

        const string after = """
            using Robust.Shared.GameObjects;
            using Robust.Shared.GameStates;
            using Robust.Shared.Serialization.Manager.Attributes;

            [NetworkedComponent]
            [AutoGenerateComponentState]
            public sealed partial class Leaky : Component
            {
                [DataField]
                public int Value;
            }
            """;

        await Verify(before, after,
            new DiagnosticResult("HONK0012", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithSpan(ForkPath, 6, 21, 6, 26)
                .WithArguments("Leaky"));
    }

    [Test]
    public async Task AddsUsing_WhenGameStatesMissing()
    {
        const string before = """
            using Robust.Shared.GameObjects;
            using Robust.Shared.Serialization.Manager.Attributes;

            [Robust.Shared.GameStates.NetworkedComponent]
            public sealed class Leaky : Component
            {
                [DataField]
                public int Value;
            }
            """;

        const string after = """
            using Robust.Shared.GameObjects;
            using Robust.Shared.Serialization.Manager.Attributes;
            using Robust.Shared.GameStates;

            [Robust.Shared.GameStates.NetworkedComponent]
            [AutoGenerateComponentState]
            public sealed partial class Leaky : Component
            {
                [DataField]
                public int Value;
            }
            """;

        await Verify(before, after,
            new DiagnosticResult("HONK0012", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithSpan(ForkPath, 5, 21, 5, 26)
                .WithArguments("Leaky"));
    }
}
