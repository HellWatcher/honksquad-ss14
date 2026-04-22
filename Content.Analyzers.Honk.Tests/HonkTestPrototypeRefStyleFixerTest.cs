using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;

namespace Content.Analyzers.Honk.Tests;

using VerifyCS = CSharpCodeFixTest<HonkTestPrototypeRefStyleAnalyzer, HonkTestPrototypeRefStyleFixer, DefaultVerifier>;

[TestFixture]
public sealed class HonkTestPrototypeRefStyleFixerTest
{
    private const string Stubs = """
        namespace Robust.Shared.Prototypes
        {
            public readonly struct ProtoId<T> where T : class, IPrototype
            {
                public readonly string Id;
                public ProtoId(string id) { Id = id; }
                public static implicit operator ProtoId<T>(string id) => new(id);
            }
            public interface IPrototype { }
            public sealed class TagPrototype : IPrototype { }
        }
        namespace Robust.UnitTesting
        {
            [System.AttributeUsage(System.AttributeTargets.Field)]
            public sealed class TestPrototypesAttribute : System.Attribute { }
        }
        """;

    private const string ForkPath = "/0/RussStation/FooTest.cs";

    private static Task Verify(string before, string after, params DiagnosticResult[] expected)
    {
        var test = new VerifyCS
        {
            TestState = { Sources = { Stubs, (ForkPath, before) } },
            FixedState = { Sources = { Stubs, (ForkPath, after) } },
        };
        test.SolutionTransforms.Add((solution, projectId) =>
            solution.WithProjectAssemblyName(projectId, "Content.IntegrationTests"));
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Test]
    public async Task PublicProtoIdField_RewrittenToConstString()
    {
        const string before = """
            using Robust.Shared.Prototypes;
            using Robust.UnitTesting;

            public sealed class Foo
            {
                [TestPrototypes]
                private const string Prototypes = "- type: Tag\n  id: DirectDummy";

                public ProtoId<TagPrototype> MyRef = "DirectDummy";
            }
            """;

        const string after = """
            using Robust.Shared.Prototypes;
            using Robust.UnitTesting;

            public sealed class Foo
            {
                [TestPrototypes]
                private const string Prototypes = "- type: Tag\n  id: DirectDummy";

                public const string MyRef = "DirectDummy";
            }
            """;

        await Verify(before, after,
            new DiagnosticResult("HONK0014", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithSpan(ForkPath, 9, 42, 9, 55)
                .WithArguments("DirectDummy"));
    }

    [Test]
    public async Task ReadOnlyModifier_StrippedInFavorOfConst()
    {
        const string before = """
            using Robust.Shared.Prototypes;
            using Robust.UnitTesting;

            public sealed class Foo
            {
                [TestPrototypes]
                private const string Prototypes = "- type: Tag\n  id: DirectDummy";

                private readonly ProtoId<TagPrototype> _myRef = "DirectDummy";
            }
            """;

        const string after = """
            using Robust.Shared.Prototypes;
            using Robust.UnitTesting;

            public sealed class Foo
            {
                [TestPrototypes]
                private const string Prototypes = "- type: Tag\n  id: DirectDummy";

                private const string _myRef = "DirectDummy";
            }
            """;

        await Verify(before, after,
            new DiagnosticResult("HONK0014", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithSpan(ForkPath, 9, 53, 9, 66)
                .WithArguments("DirectDummy"));
    }
}
