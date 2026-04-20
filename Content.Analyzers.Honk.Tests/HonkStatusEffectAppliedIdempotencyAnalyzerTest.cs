using System.Threading.Tasks;
using Content.Analyzers.Honk;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;

namespace Content.Analyzers.Honk.Tests;

using VerifyCS = CSharpAnalyzerTest<HonkStatusEffectAppliedIdempotencyAnalyzer, DefaultVerifier>;

[TestFixture]
public sealed class HonkStatusEffectAppliedIdempotencyAnalyzerTest
{
    private const string Stubs = """
        namespace Robust.Shared.GameObjects
        {
            public abstract class Component { }
            public abstract class EntitySystem
            {
                public void SubscribeLocalEvent<TComp, TEvent>(System.Action<Microsoft.CodeAnalysis.Testing.EntityUid, TComp, TEvent> handler) where TComp : Component { }
            }
        }
        namespace Microsoft.CodeAnalysis.Testing
        {
            public struct EntityUid { }
        }
        namespace Content.Shared.StatusEffectNew
        {
            public sealed class StatusEffectAppliedEvent { }
            public sealed class StatusEffectRemovedEvent { }
        }
        namespace Robust.Shared.Timing
        {
            public interface IGameTiming
            {
                bool ApplyingState { get; }
            }
        }
        """;

    private const string ForkPath = "Content.Shared/RussStation/ForkSystem.cs";
    private const string UpstreamPath = "Content.Shared/Upstream/Sys.cs";

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
    public async Task HandlerWithApplyingStateGuard_DoesNotReport()
    {
        const string code = """
            using Robust.Shared.GameObjects;
            using Robust.Shared.Timing;
            using Content.Shared.StatusEffectNew;

            public sealed class FooComponent : Component { }

            public sealed class FooSystem : EntitySystem
            {
                private IGameTiming _timing = null!;

                public void Init()
                {
                    SubscribeLocalEvent<FooComponent, StatusEffectAppliedEvent>(OnApplied);
                }

                private void OnApplied(Microsoft.CodeAnalysis.Testing.EntityUid uid, FooComponent comp, StatusEffectAppliedEvent ev)
                {
                    if (_timing.ApplyingState)
                        return;
                }
            }
            """;

        await Verify(code, ForkPath);
    }

    [Test]
    public async Task HandlerWithoutGuard_Reports()
    {
        const string code = """
            using Robust.Shared.GameObjects;
            using Content.Shared.StatusEffectNew;

            public sealed class FooComponent : Component { }

            public sealed class FooSystem : EntitySystem
            {
                public void Init()
                {
                    SubscribeLocalEvent<FooComponent, StatusEffectAppliedEvent>(OnApplied);
                }

                private void OnApplied(Microsoft.CodeAnalysis.Testing.EntityUid uid, FooComponent comp, StatusEffectAppliedEvent ev)
                {
                    var x = 1;
                }
            }
            """;

        await Verify(code, ForkPath,
            new DiagnosticResult("HONK0013", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithSpan(ForkPath, 10, 69, 10, 78)
                .WithArguments("OnApplied"));
    }

    [Test]
    public async Task LambdaWithoutGuard_Reports()
    {
        const string code = """
            using Robust.Shared.GameObjects;
            using Content.Shared.StatusEffectNew;

            public sealed class FooComponent : Component { }

            public sealed class FooSystem : EntitySystem
            {
                public void Init()
                {
                    SubscribeLocalEvent<FooComponent, StatusEffectAppliedEvent>((uid, comp, ev) => { var x = 1; });
                }
            }
            """;

        await Verify(code, ForkPath,
            new DiagnosticResult("HONK0013", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithSpan(ForkPath, 10, 69, 10, 102)
                .WithArguments("<lambda>"));
    }

    [Test]
    public async Task DifferentEvent_DoesNotReport()
    {
        const string code = """
            using Robust.Shared.GameObjects;
            using Content.Shared.StatusEffectNew;

            public sealed class FooComponent : Component { }

            public sealed class FooSystem : EntitySystem
            {
                public void Init()
                {
                    SubscribeLocalEvent<FooComponent, StatusEffectRemovedEvent>(OnRemoved);
                }

                private void OnRemoved(Microsoft.CodeAnalysis.Testing.EntityUid uid, FooComponent comp, StatusEffectRemovedEvent ev)
                {
                    var x = 1;
                }
            }
            """;

        await Verify(code, ForkPath);
    }

    [Test]
    public async Task HandlerWithInlineApplyingStateCheck_DoesNotReport()
    {
        const string code = """
            using Robust.Shared.GameObjects;
            using Robust.Shared.Timing;
            using Content.Shared.StatusEffectNew;

            public sealed class FooComponent : Component { }

            public sealed class FooSystem : EntitySystem
            {
                private IGameTiming _timing = null!;

                public void Init()
                {
                    SubscribeLocalEvent<FooComponent, StatusEffectAppliedEvent>(OnApplied);
                }

                private void OnApplied(Microsoft.CodeAnalysis.Testing.EntityUid uid, FooComponent comp, StatusEffectAppliedEvent ev)
                {
                    if (_timing.ApplyingState && true) return;
                    var x = 1;
                }
            }
            """;

        await Verify(code, ForkPath);
    }

    [Test]
    public async Task HandlerWithoutGuard_InUpstreamFile_DoesNotReport()
    {
        const string code = """
            using Robust.Shared.GameObjects;
            using Content.Shared.StatusEffectNew;

            public sealed class FooComponent : Component { }

            public sealed class FooSystem : EntitySystem
            {
                public void Init()
                {
                    SubscribeLocalEvent<FooComponent, StatusEffectAppliedEvent>(OnApplied);
                }

                private void OnApplied(Microsoft.CodeAnalysis.Testing.EntityUid uid, FooComponent comp, StatusEffectAppliedEvent ev)
                {
                    var x = 1;
                }
            }
            """;

        await Verify(code, UpstreamPath);
    }
}
