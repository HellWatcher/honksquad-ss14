using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;

namespace Content.Analyzers.Honk.Tests;

using VerifyCS = CSharpAnalyzerTest<HonkEagerStringInTickLoopAnalyzer, DefaultVerifier>;

[TestFixture]
public sealed class HonkEagerStringInTickLoopAnalyzerTest
{
    private const string Stubs = """
        namespace Robust.Shared.GameObjects
        {
            public abstract class EntitySystem { }
        }
        public static class Loc
        {
            public static string GetString(string s) => s;
            public static string GetString(string s, params (string, object)[] a) => s;
        }
        public struct StubQuery
        {
            public bool MoveNext() => false;
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
    public async Task LocGetString_InMoveNextLoop_InUpdate_ForkFile_Reports()
    {
        const string code = """
            using Robust.Shared.GameObjects;

            public sealed class FooSystem : EntitySystem
            {
                private StubQuery _query;

                public override void Update(float frameTime)
                {
                    while (_query.MoveNext())
                    {
                        Loc.GetString("x");
                    }
                }
            }
            """;

        await Verify(code, "Content.Server/RussStation/Foo/FooSystem.cs",
            new DiagnosticResult("HONK0027", DiagnosticSeverity.Info)
                .WithSpan("Content.Server/RussStation/Foo/FooSystem.cs", 11, 13, 11, 31)
                .WithArguments("Loc.GetString"));
    }

    [Test]
    public async Task LocGetString_OutsideLoop_InUpdate_DoesNotReport()
    {
        const string code = """
            using Robust.Shared.GameObjects;

            public sealed class FooSystem : EntitySystem
            {
                private StubQuery _query;

                public override void Update(float frameTime)
                {
                    Loc.GetString("x");
                    while (_query.MoveNext())
                    {
                    }
                }
            }
            """;

        await Verify(code, "Content.Server/RussStation/Foo/FooSystem.cs");
    }

    [Test]
    public async Task LocGetString_InMoveNextLoop_NotUpdateMethod_DoesNotReport()
    {
        const string code = """
            using Robust.Shared.GameObjects;

            public sealed class FooSystem : EntitySystem
            {
                private StubQuery _query;

                public void Tick(float frameTime)
                {
                    while (_query.MoveNext())
                    {
                        Loc.GetString("x");
                    }
                }
            }
            """;

        await Verify(code, "Content.Server/RussStation/Foo/FooSystem.cs");
    }

    [Test]
    public async Task LocGetString_InMoveNextLoop_InUpdate_UpstreamFile_DoesNotReport()
    {
        const string code = """
            using Robust.Shared.GameObjects;

            public sealed class FooSystem : EntitySystem
            {
                private StubQuery _query;

                public override void Update(float frameTime)
                {
                    while (_query.MoveNext())
                    {
                        Loc.GetString("x");
                    }
                }
            }
            """;

        await Verify(code, "Content.Server/Foo/FooSystem.cs");
    }

    [Test]
    public async Task LocGetString_InMoveNextLoop_InUpdate_HonkPartialFile_Reports()
    {
        const string code = """
            using Robust.Shared.GameObjects;

            public sealed class FooSystem : EntitySystem
            {
                private StubQuery _query;

                public override void Update(float frameTime)
                {
                    while (_query.MoveNext())
                    {
                        Loc.GetString("x");
                    }
                }
            }
            """;

        await Verify(code, "Content.Server/Foo/FooSystem.Honk.cs",
            new DiagnosticResult("HONK0027", DiagnosticSeverity.Info)
                .WithSpan("Content.Server/Foo/FooSystem.Honk.cs", 11, 13, 11, 31)
                .WithArguments("Loc.GetString"));
    }
}
