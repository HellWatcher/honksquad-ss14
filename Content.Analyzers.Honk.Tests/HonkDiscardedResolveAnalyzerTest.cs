using System.Threading.Tasks;
using Content.Analyzers.Honk;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;

namespace Content.Analyzers.Honk.Tests;

using VerifyCS = CSharpAnalyzerTest<HonkDiscardedResolveAnalyzer, DefaultVerifier>;

[TestFixture]
public sealed class HonkDiscardedResolveAnalyzerTest
{
    private const string Stubs = """
        namespace Robust.Shared.GameObjects
        {
            public abstract class EntitySystem
            {
                protected bool Resolve<T>(int uid, ref T? comp, bool logMissing = true) => true;
            }
            public sealed class FooComponent
            {
            }
        }
        """;

    private const string ForkPath = "Content.Shared/RussStation/SomeForkSystem.cs";
    private const string UpstreamPath = "Content.Shared/Upstream/SomeSystem.cs";

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
    public async Task DiscardedResolve_InEntitySystem_ForkFile_Reports()
    {
        const string code = """
            using Robust.Shared.GameObjects;

            public sealed class MySystem : EntitySystem
            {
                public void Foo(int uid, ref FooComponent? comp)
                {
                    Resolve(uid, ref comp);
                }
            }
            """;

        await Verify(code, ForkPath,
            new DiagnosticResult("HONK0030", DiagnosticSeverity.Warning)
                .WithSpan(ForkPath, 7, 9, 7, 31));
    }

    [Test]
    public async Task ResolveInIfCondition_DoesNotReport()
    {
        const string code = """
            using Robust.Shared.GameObjects;

            public sealed class MySystem : EntitySystem
            {
                public void Foo(int uid, ref FooComponent? comp)
                {
                    if (!Resolve(uid, ref comp))
                        return;
                }
            }
            """;

        await Verify(code, ForkPath);
    }

    [Test]
    public async Task ResolveAssignedToLocal_DoesNotReport()
    {
        const string code = """
            using Robust.Shared.GameObjects;

            public sealed class MySystem : EntitySystem
            {
                public void Foo(int uid, ref FooComponent? comp)
                {
                    var ok = Resolve(uid, ref comp);
                }
            }
            """;

        await Verify(code, ForkPath);
    }

    [Test]
    public async Task DiscardedResolve_OutsideEntitySystem_DoesNotReport()
    {
        const string code = """
            using Robust.Shared.GameObjects;

            public sealed class SomeHelper
            {
                public void Foo(int uid, ref FooComponent? comp)
                {
                    Resolve(uid, ref comp);
                }

                private bool Resolve(int uid, ref FooComponent? comp) => true;
            }
            """;

        await Verify(code, ForkPath);
    }

    [Test]
    public async Task DiscardedResolve_InUpstreamFile_DoesNotReport()
    {
        const string code = """
            using Robust.Shared.GameObjects;

            public sealed class MySystem : EntitySystem
            {
                public void Foo(int uid, ref FooComponent? comp)
                {
                    Resolve(uid, ref comp);
                }
            }
            """;

        await Verify(code, UpstreamPath);
    }

    [Test]
    public async Task DiscardedResolve_InHonkPartial_Reports()
    {
        const string honkPath = "Content.Shared/Upstream/SomeSystem.Honk.cs";
        const string code = """
            using Robust.Shared.GameObjects;

            public sealed class HonkPartialSystem : EntitySystem
            {
                public void Foo(int uid, ref FooComponent? comp)
                {
                    Resolve(uid, ref comp);
                }
            }
            """;

        await Verify(code, honkPath,
            new DiagnosticResult("HONK0030", DiagnosticSeverity.Warning)
                .WithSpan(honkPath, 7, 9, 7, 31));
    }
}
