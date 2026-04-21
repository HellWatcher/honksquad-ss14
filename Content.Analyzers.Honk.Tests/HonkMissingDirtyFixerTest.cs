using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;

namespace Content.Analyzers.Honk.Tests;

using VerifyCS = CSharpCodeFixTest<HonkMissingDirtyAnalyzer, HonkMissingDirtyFixer, DefaultVerifier>;

[TestFixture]
public sealed class HonkMissingDirtyFixerTest
{
    private const string Stubs = """
        namespace Robust.Shared.GameStates
        {
            [System.AttributeUsage(System.AttributeTargets.Class, Inherited = true)]
            public sealed class NetworkedComponentAttribute : System.Attribute { }
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
            public readonly struct EntityUid { }

            public abstract class EntitySystem
            {
                protected void Dirty(EntityUid uid, IComponent comp) { }
            }
        }
        namespace Fork.Stubs
        {
            using Robust.Shared.GameObjects;
            using Robust.Shared.GameStates;

            [NetworkedComponent]
            public sealed class NetComp : Component
            {
                public int Value;
            }
        }
        """;

    private const string ForkPath = "Content.Shared/RussStation/NetworkingStuff.cs";

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
    public async Task InsertsDirtyCallAfterWrite()
    {
        const string before = """
            using Robust.Shared.GameObjects;
            using Fork.Stubs;

            public sealed class MySys : EntitySystem
            {
                public void Poke(EntityUid uid, NetComp comp)
                {
                    comp.Value = 5;
                }
            }
            """;

        const string after = """
            using Robust.Shared.GameObjects;
            using Fork.Stubs;

            public sealed class MySys : EntitySystem
            {
                public void Poke(EntityUid uid, NetComp comp)
                {
                    comp.Value = 5;
                    Dirty(uid, comp);
                }
            }
            """;

        await Verify(before, after,
            new DiagnosticResult("HONK0010", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithSpan(ForkPath, 8, 9, 8, 23)
                .WithArguments("Value", "NetComp"));
    }

    [Test]
    public async Task BailsWhenNoEntityUidParameterInScope()
    {
        const string before = """
            using Robust.Shared.GameObjects;
            using Fork.Stubs;

            public sealed class MySys : EntitySystem
            {
                public void Poke(NetComp comp)
                {
                    comp.Value = 5;
                }
            }
            """;

        await Verify(before, before,
            new DiagnosticResult("HONK0010", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithSpan(ForkPath, 8, 9, 8, 23)
                .WithArguments("Value", "NetComp"));
    }
}
