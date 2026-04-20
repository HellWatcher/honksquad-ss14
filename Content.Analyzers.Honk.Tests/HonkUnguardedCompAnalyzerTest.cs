using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;

namespace Content.Analyzers.Honk.Tests;

using VerifyCS = CSharpAnalyzerTest<HonkUnguardedCompAnalyzer, DefaultVerifier>;

[TestFixture]
public sealed class HonkUnguardedCompAnalyzerTest
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
            public sealed class OtherComp : IComponent { }
            public sealed class SomeEvent
            {
                public EntityUid Target;
                public EntityUid User;
                public EntityUid OtherEntity;
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
    public async Task UnguardedCompOnArgsTarget_Reports()
    {
        const string code = """
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

        await Verify(code, ForkPath,
            new DiagnosticResult("HONK0011", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithSpan(ForkPath, 8, 17, 8, 46)
                .WithArguments("TargetComp", "args.Target"));
    }

    [Test]
    public async Task HasCompGuard_DoesNotReport()
    {
        const string code = """
            using Robust.Shared.GameObjects;
            using Fork.Stubs;

            public sealed class Sys : EntitySystem
            {
                public void OnIt(SomeEvent args)
                {
                    if (!HasComp<TargetComp>(args.Target))
                        return;
                    var c = Comp<TargetComp>(args.Target);
                }
            }
            """;

        await Verify(code, ForkPath);
    }

    [Test]
    public async Task TryCompGuard_DoesNotReport()
    {
        const string code = """
            using Robust.Shared.GameObjects;
            using Fork.Stubs;

            public sealed class Sys : EntitySystem
            {
                public void OnIt(SomeEvent args)
                {
                    if (!TryComp<TargetComp>(args.Target, out _))
                    {
                        var c = Comp<TargetComp>(args.Target);
                    }
                }
            }
            """;

        await Verify(code, ForkPath);
    }

    [Test]
    public async Task CompOnLocalUid_DoesNotReport()
    {
        const string code = """
            using Robust.Shared.GameObjects;
            using Fork.Stubs;

            public sealed class Sys : EntitySystem
            {
                public void OnIt(SomeEvent args)
                {
                    var local = args.Target;
                    var c = Comp<TargetComp>(local);
                }
            }
            """;

        await Verify(code, ForkPath);
    }

    [Test]
    public async Task DifferentComponentGuard_StillReports()
    {
        const string code = """
            using Robust.Shared.GameObjects;
            using Fork.Stubs;

            public sealed class Sys : EntitySystem
            {
                public void OnIt(SomeEvent args)
                {
                    if (!HasComp<OtherComp>(args.Target))
                        return;
                    var c = Comp<TargetComp>(args.Target);
                }
            }
            """;

        await Verify(code, ForkPath,
            new DiagnosticResult("HONK0011", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithSpan(ForkPath, 10, 17, 10, 46)
                .WithArguments("TargetComp", "args.Target"));
    }

    [Test]
    public async Task NonEntitySystem_DoesNotReport()
    {
        const string code = """
            using Robust.Shared.GameObjects;
            using Fork.Stubs;

            public sealed class Plain
            {
                public T Comp<T>(EntityUid uid) where T : IComponent => default!;
                public void OnIt(SomeEvent args)
                {
                    var c = Comp<TargetComp>(args.Target);
                }
            }
            """;

        await Verify(code, ForkPath);
    }

    [Test]
    public async Task UnguardedCompInUpstreamFile_DoesNotReport()
    {
        const string code = """
            using Robust.Shared.GameObjects;
            using Fork.Stubs;

            public sealed class UpstreamSys : EntitySystem
            {
                public void OnIt(SomeEvent args)
                {
                    var c = Comp<TargetComp>(args.Target);
                }
            }
            """;

        await Verify(code, UpstreamPath);
    }
}
