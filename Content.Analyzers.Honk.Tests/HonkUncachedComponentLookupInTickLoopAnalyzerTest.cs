using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;

namespace Content.Analyzers.Honk.Tests;

using VerifyCS = CSharpAnalyzerTest<HonkUncachedComponentLookupInTickLoopAnalyzer, DefaultVerifier>;

[TestFixture]
public sealed class HonkUncachedComponentLookupInTickLoopAnalyzerTest
{
    private const string Stubs = """
        namespace Fork.Stubs
        {
            public sealed class FooComponent { }

            public struct StubEnumerator
            {
                public bool MoveNext() => false;
            }

            public abstract class StubSystem
            {
                protected StubEnumerator _q;

                protected bool TryComp<T>(int uid, out T? comp) { comp = default; return true; }
                protected bool HasComp<T>(int uid) => true;
                protected T Comp<T>(int uid) => default!;

                public virtual void Update(float dt) { }
                public virtual void FrameUpdate(float dt) { }
            }
        }
        """;

    private const string ForkPath = "Content.Shared/RussStation/ForkSystem.cs";
    private const string UpstreamPath = "Content.Shared/Upstream/Sys.cs";

    private static Task Verify(string code, string filePath, params DiagnosticResult[] expected)
    {
        var test = new VerifyCS { TestState = { Sources = { Stubs, (filePath, code) } } };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Test]
    public async Task TryCompInUpdateLoop_Reports()
    {
        const string code = """
            using Fork.Stubs;

            public sealed class Sys : StubSystem
            {
                public override void Update(float dt)
                {
                    int uid = 0;
                    while (_q.MoveNext())
                    {
                        TryComp<FooComponent>(uid, out var c);
                    }
                }
            }
            """;

        await Verify(code, ForkPath,
            new DiagnosticResult("HONK0032", DiagnosticSeverity.Warning)
                .WithSpan(ForkPath, 10, 13, 10, 50)
                .WithArguments("TryComp", "FooComponent"));
    }

    [Test]
    public async Task TryCompOutsideLoop_DoesNotReport()
    {
        const string code = """
            using Fork.Stubs;

            public sealed class Sys : StubSystem
            {
                public override void Update(float dt)
                {
                    int uid = 0;
                    TryComp<FooComponent>(uid, out var c);
                }
            }
            """;

        await Verify(code, ForkPath);
    }

    [Test]
    public async Task LoopInNonUpdateMethod_DoesNotReport()
    {
        const string code = """
            using Fork.Stubs;

            public sealed class Sys : StubSystem
            {
                public void Tick(float dt)
                {
                    int uid = 0;
                    while (_q.MoveNext())
                    {
                        TryComp<FooComponent>(uid, out var c);
                    }
                }
            }
            """;

        await Verify(code, ForkPath);
    }

    [Test]
    public async Task TryCompInUpdateLoop_UpstreamFile_DoesNotReport()
    {
        const string code = """
            using Fork.Stubs;

            public sealed class Sys : StubSystem
            {
                public override void Update(float dt)
                {
                    int uid = 0;
                    while (_q.MoveNext())
                    {
                        TryComp<FooComponent>(uid, out var c);
                    }
                }
            }
            """;

        await Verify(code, UpstreamPath);
    }

    [Test]
    public async Task TryCompInUpdateLoop_HonkPartialFile_Reports()
    {
        const string code = """
            using Fork.Stubs;

            public sealed class Sys : StubSystem
            {
                public override void Update(float dt)
                {
                    int uid = 0;
                    while (_q.MoveNext())
                    {
                        TryComp<FooComponent>(uid, out var c);
                    }
                }
            }
            """;

        await Verify(code, "Content.Shared/Foo/ForkSystem.Honk.cs",
            new DiagnosticResult("HONK0032", DiagnosticSeverity.Warning)
                .WithSpan("Content.Shared/Foo/ForkSystem.Honk.cs", 10, 13, 10, 50)
                .WithArguments("TryComp", "FooComponent"));
    }
}
