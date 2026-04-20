using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;

namespace Content.Analyzers.Honk.Tests;

using VerifyCS = CSharpAnalyzerTest<HonkMissingDirtyAnalyzer, DefaultVerifier>;

[TestFixture]
public sealed class HonkMissingDirtyAnalyzerTest
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
                protected void DirtyField<T>(EntityUid uid, T comp, string field) where T : IComponent { }
                protected void DirtyFields<T>(EntityUid uid, T comp, params string[] fields) where T : IComponent { }
                protected void DirtyEntity(EntityUid uid) { }
            }
        }
        namespace Fork.Stubs
        {
            using Robust.Shared.GameObjects;
            using Robust.Shared.GameStates;
            using Robust.Shared.Analyzers;

            [NetworkedComponent]
            public sealed class NetComp : Component
            {
                public int Value;
                [AutoNetworkedField]
                public int Auto;
            }

            public sealed class PlainComp : Component
            {
                public int Value;
            }
        }
        """;

    private const string ForkPath = "Content.Shared/RussStation/NetworkingStuff.cs";
    private const string UpstreamPath = "Content.Shared/Upstream/NetworkingStuff.cs";

    private static Task Verify(string code, string filePath, params DiagnosticResult[] expected)
    {
        var test = new VerifyCS
        {
            TestState = { Sources = { Stubs, (filePath, code) } },
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Test]
    public async Task WriteWithoutDirty_Reports()
    {
        const string code = """
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

        await Verify(code, ForkPath,
            new DiagnosticResult("HONK0010", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithSpan(ForkPath, 8, 9, 8, 23)
                .WithArguments("Value", "NetComp"));
    }

    [Test]
    public async Task WriteFollowedByDirty_DoesNotReport()
    {
        const string code = """
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

        await Verify(code, ForkPath);
    }

    [Test]
    public async Task WriteFollowedByDirtyField_DoesNotReport()
    {
        const string code = """
            using Robust.Shared.GameObjects;
            using Fork.Stubs;

            public sealed class MySys : EntitySystem
            {
                public void Poke(EntityUid uid, NetComp comp)
                {
                    comp.Value = 5;
                    DirtyField(uid, comp, nameof(NetComp.Value));
                }
            }
            """;

        await Verify(code, ForkPath);
    }

    [Test]
    public async Task AutoNetworkedFieldWrite_DoesNotReport()
    {
        const string code = """
            using Robust.Shared.GameObjects;
            using Fork.Stubs;

            public sealed class MySys : EntitySystem
            {
                public void Poke(EntityUid uid, NetComp comp)
                {
                    comp.Auto = 5;
                }
            }
            """;

        await Verify(code, ForkPath);
    }

    [Test]
    public async Task NonNetworkedComponentWrite_DoesNotReport()
    {
        const string code = """
            using Robust.Shared.GameObjects;
            using Fork.Stubs;

            public sealed class MySys : EntitySystem
            {
                public void Poke(EntityUid uid, PlainComp comp)
                {
                    comp.Value = 5;
                }
            }
            """;

        await Verify(code, ForkPath);
    }

    [Test]
    public async Task WriteWithoutDirty_InUpstreamFile_DoesNotReport()
    {
        const string code = """
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

        await Verify(code, UpstreamPath);
    }
}
