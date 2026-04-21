using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;

namespace Content.Analyzers.Honk.Tests;

using VerifyCS = CSharpCodeFixTest<HonkUnguardedCompAnalyzer, HonkUnguardedCompFixer, DefaultVerifier>;

[TestFixture]
public sealed class HonkUnguardedCompFixerTest
{
    private const string Stubs = """
        namespace Robust.Shared.GameObjects
        {
            public interface IComponent { }
            public readonly struct EntityUid { }

            public abstract class EntitySystem
            {
                protected T Comp<T>(EntityUid uid) where T : IComponent => default!;
                protected bool HasComp<T>(EntityUid uid) where T : IComponent => false;
                protected bool TryComp<T>(EntityUid uid, out T? comp) where T : IComponent
                { comp = default; return false; }
            }
        }
        namespace Fork.Stubs
        {
            using Robust.Shared.GameObjects;
            public sealed class TargetComp : IComponent { }
            public sealed class SomeEvent
            {
                public EntityUid Target;
            }
        }
        """;

    private const string ForkPath = "Content.Shared/RussStation/ForkSystem.cs";

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
    public async Task WrapsLocalDeclInTryCompEarlyReturn()
    {
        const string before = """
            using Robust.Shared.GameObjects;
            using Fork.Stubs;

            public sealed class Sys : EntitySystem
            {
                public void OnIt(SomeEvent args)
                {
                    var c = Comp<TargetComp>(args.Target);
                }
            }
            """;

        const string after = """
            using Robust.Shared.GameObjects;
            using Fork.Stubs;

            public sealed class Sys : EntitySystem
            {
                public void OnIt(SomeEvent args)
                {
                    if (!TryComp<TargetComp>(args.Target, out var c))
                        return;
                }
            }
            """;

        await Verify(before, after,
            new DiagnosticResult("HONK0011", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithSpan(ForkPath, 8, 17, 8, 46)
                .WithArguments("TargetComp", "args.Target"));
    }

    [Test]
    public async Task BailsOnNonVoidMethod()
    {
        const string before = """
            using Robust.Shared.GameObjects;
            using Fork.Stubs;

            public sealed class Sys : EntitySystem
            {
                public int OnIt(SomeEvent args)
                {
                    var c = Comp<TargetComp>(args.Target);
                    return 0;
                }
            }
            """;

        await Verify(before, before,
            new DiagnosticResult("HONK0011", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithSpan(ForkPath, 8, 17, 8, 46)
                .WithArguments("TargetComp", "args.Target"));
    }
}
