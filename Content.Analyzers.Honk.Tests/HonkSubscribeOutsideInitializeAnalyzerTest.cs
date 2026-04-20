using System.Threading.Tasks;
using Content.Analyzers.Honk;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;

namespace Content.Analyzers.Honk.Tests;

using VerifyCS = CSharpAnalyzerTest<HonkSubscribeOutsideInitializeAnalyzer, DefaultVerifier>;

[TestFixture]
public sealed class HonkSubscribeOutsideInitializeAnalyzerTest
{
    private const string Stubs = """
        namespace Robust.Shared.GameObjects
        {
            public abstract class EntitySystem
            {
                public virtual void Initialize() { }
                public virtual void Update(float frameTime) { }
                protected void SubscribeLocalEvent<TComp, TEvent>(ComponentEventHandler<TComp, TEvent> handler)
                    where TComp : IComponent
                    where TEvent : notnull
                { }
                protected void SubscribeNetworkEvent<TEvent>(EventHandler<TEvent> handler)
                    where TEvent : notnull
                { }
                protected void SubscribeAllEvent<TEvent>(EventHandler<TEvent> handler)
                    where TEvent : notnull
                { }
            }
            public interface IComponent { }
            public delegate void ComponentEventHandler<TComp, TEvent>(System.Guid uid, TComp comp, TEvent args)
                where TComp : IComponent
                where TEvent : notnull;
            public delegate void EventHandler<TEvent>(TEvent args) where TEvent : notnull;
            public sealed class SomeComponent : IComponent { }
            public sealed class SomeEvent { }
        }
        """;

    private const string ForkPath = "Content.Shared/RussStation/SomeForkSystem.cs";
    private const string UpstreamPath = "Content.Shared/Upstream/SomeSystem.cs";

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
    public async Task SubscribeInInitialize_DoesNotReport()
    {
        const string code = """
            using Robust.Shared.GameObjects;

            public sealed class MySystem : EntitySystem
            {
                public override void Initialize()
                {
                    base.Initialize();
                    SubscribeLocalEvent<SomeComponent, SomeEvent>(OnSome);
                }

                private void OnSome(System.Guid uid, SomeComponent comp, SomeEvent args) { }
            }
            """;

        await Verify(code, ForkPath);
    }

    [Test]
    public async Task SubscribeInArbitraryHelper_Reports()
    {
        const string code = """
            using Robust.Shared.GameObjects;

            public sealed class MySystem : EntitySystem
            {
                public override void Initialize()
                {
                    base.Initialize();
                    WireUp();
                }

                private void WireUp()
                {
                    SubscribeLocalEvent<SomeComponent, SomeEvent>(OnSome);
                }

                private void OnSome(System.Guid uid, SomeComponent comp, SomeEvent args) { }
            }
            """;

        await Verify(code, ForkPath,
            new DiagnosticResult("HONK0009", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithSpan(ForkPath, 13, 9, 13, 62)
                .WithArguments("SubscribeLocalEvent"));
    }

    [Test]
    public async Task SubscribeInInitializeXxxPartialHelper_DoesNotReport()
    {
        // Upstream convention: Initialize() chains to InitializeBuckle(),
        // InitializeStrap(), etc. in partial files.
        const string code = """
            using Robust.Shared.GameObjects;

            public sealed class MySystem : EntitySystem
            {
                public override void Initialize()
                {
                    base.Initialize();
                    InitializeBuckle();
                }

                private void InitializeBuckle()
                {
                    SubscribeLocalEvent<SomeComponent, SomeEvent>(OnSome);
                }

                private void OnSome(System.Guid uid, SomeComponent comp, SomeEvent args) { }
            }
            """;

        await Verify(code, ForkPath);
    }

    [Test]
    public async Task SubscribeNetworkEventInUpdate_Reports()
    {
        const string code = """
            using Robust.Shared.GameObjects;

            public sealed class MySystem : EntitySystem
            {
                public override void Update(float frameTime)
                {
                    SubscribeNetworkEvent<SomeEvent>(OnSome);
                }

                private void OnSome(SomeEvent args) { }
            }
            """;

        await Verify(code, ForkPath,
            new DiagnosticResult("HONK0009", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithSpan(ForkPath, 7, 9, 7, 49)
                .WithArguments("SubscribeNetworkEvent"));
    }

    [Test]
    public async Task SubscribeOutsideEntitySystem_DoesNotReport()
    {
        const string code = """
            using Robust.Shared.GameObjects;

            public sealed class NotASystem
            {
                public void DoThing()
                {
                    // Lookalike method, not the EntitySystem one.
                    SubscribeLocalEvent();
                }

                private void SubscribeLocalEvent() { }
            }
            """;

        await Verify(code, ForkPath);
    }

    [Test]
    public async Task SubscribeInTransitivelyDerivedSystem_Reports()
    {
        const string code = """
            using Robust.Shared.GameObjects;

            public abstract class SharedFooSystem : EntitySystem { }

            public sealed class FooSystem : SharedFooSystem
            {
                private void Helper()
                {
                    SubscribeLocalEvent<SomeComponent, SomeEvent>(OnSome);
                }

                private void OnSome(System.Guid uid, SomeComponent comp, SomeEvent args) { }
            }
            """;

        await Verify(code, ForkPath,
            new DiagnosticResult("HONK0009", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithSpan(ForkPath, 9, 9, 9, 62)
                .WithArguments("SubscribeLocalEvent"));
    }

    [Test]
    public async Task SubscribeInUpstreamFile_DoesNotReport()
    {
        const string code = """
            using Robust.Shared.GameObjects;

            public sealed class UpstreamSystem : EntitySystem
            {
                public override void Update(float frameTime)
                {
                    SubscribeLocalEvent<SomeComponent, SomeEvent>(OnSome);
                }

                private void OnSome(System.Guid uid, SomeComponent comp, SomeEvent args) { }
            }
            """;

        await Verify(code, UpstreamPath);
    }
}
