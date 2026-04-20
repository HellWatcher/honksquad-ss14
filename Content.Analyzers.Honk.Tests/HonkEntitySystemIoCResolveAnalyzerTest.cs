using System.Threading.Tasks;
using Content.Analyzers.Honk;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;

namespace Content.Analyzers.Honk.Tests;

using VerifyCS = CSharpAnalyzerTest<HonkEntitySystemIoCResolveAnalyzer, DefaultVerifier>;

[TestFixture]
public sealed class HonkEntitySystemIoCResolveAnalyzerTest
{
    private const string Stubs = """
        namespace Robust.Shared.GameObjects
        {
            public abstract class EntitySystem
            {
            }
        }
        namespace Robust.Shared.IoC
        {
            public static class IoCManager
            {
                public static T Resolve<T>() => default!;
                public static IoCProxy Instance { get; } = new();
                public sealed class IoCProxy
                {
                    public T Resolve<T>() => default!;
                }
            }
        }
        namespace Robust.Shared.Timing
        {
            public interface IGameTiming { }
        }
        """;

    private static Task Verify(string code, params DiagnosticResult[] expected)
    {
        var test = new VerifyCS
        {
            TestState =
            {
                Sources = { Stubs, code },
            },
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Test]
    public async Task Resolve_InsideEntitySystem_Reports()
    {
        const string code = """
            using Robust.Shared.GameObjects;
            using Robust.Shared.IoC;
            using Robust.Shared.Timing;

            public sealed class MySystem : EntitySystem
            {
                public void Foo()
                {
                    var timing = IoCManager.Resolve<IGameTiming>();
                }
            }
            """;

        await Verify(code,
            new DiagnosticResult("HONK0007", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithSpan("/0/Test1.cs", 9, 22, 9, 55)
                .WithArguments("MySystem"));
    }

    [Test]
    public async Task ResolveViaInstance_InsideEntitySystem_Reports()
    {
        const string code = """
            using Robust.Shared.GameObjects;
            using Robust.Shared.IoC;
            using Robust.Shared.Timing;

            public sealed class MySystem : EntitySystem
            {
                public void Foo()
                {
                    var timing = IoCManager.Instance.Resolve<IGameTiming>();
                }
            }
            """;

        await Verify(code,
            new DiagnosticResult("HONK0007", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithSpan("/0/Test1.cs", 9, 22, 9, 64)
                .WithArguments("MySystem"));
    }

    [Test]
    public async Task Resolve_OutsideEntitySystem_DoesNotReport()
    {
        const string code = """
            using Robust.Shared.IoC;
            using Robust.Shared.Timing;

            public sealed class SomeHelper
            {
                public void Foo()
                {
                    var timing = IoCManager.Resolve<IGameTiming>();
                }
            }
            """;

        await Verify(code);
    }

    [Test]
    public async Task EntitySystem_WithoutResolve_DoesNotReport()
    {
        const string code = """
            using Robust.Shared.GameObjects;
            using Robust.Shared.Timing;

            public sealed class MySystem : EntitySystem
            {
                private IGameTiming _timing = default!;

                public void Foo()
                {
                    _ = _timing;
                }
            }
            """;

        await Verify(code);
    }

    [Test]
    public async Task Resolve_InTransitivelyDerivedSystem_Reports()
    {
        const string code = """
            using Robust.Shared.GameObjects;
            using Robust.Shared.IoC;
            using Robust.Shared.Timing;

            public abstract class SharedFooSystem : EntitySystem { }

            public sealed class FooSystem : SharedFooSystem
            {
                public void Bar()
                {
                    var timing = IoCManager.Resolve<IGameTiming>();
                }
            }
            """;

        await Verify(code,
            new DiagnosticResult("HONK0007", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithSpan("/0/Test1.cs", 11, 22, 11, 55)
                .WithArguments("FooSystem"));
    }
}
