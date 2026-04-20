using System.Threading.Tasks;
using Content.Analyzers.Honk;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;

namespace Content.Analyzers.Honk.Tests;

using VerifyCS = CSharpAnalyzerTest<HonkTestPrototypeRefStyleAnalyzer, DefaultVerifier>;

[TestFixture]
public sealed class HonkTestPrototypeRefStyleAnalyzerTest
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

    private static Task Verify(string code, string assemblyName, params DiagnosticResult[] expected)
    {
        var test = new VerifyCS
        {
            TestState = { Sources = { Stubs, code } },
        };
        test.TestState.AdditionalReferences.Clear();
        test.SolutionTransforms.Add((solution, projectId) =>
        {
            var project = solution.GetProject(projectId)!;
            return solution.WithProjectAssemblyName(projectId, assemblyName);
        });
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Test]
    public async Task TestProtoId_FieldWithTestPrototypeId_Reports()
    {
        const string code = """
            using Robust.Shared.Prototypes;
            using Robust.UnitTesting;

            public sealed class Foo
            {
                private const string MyTag = "MyTagDummy";

                [TestPrototypes]
                private const string Prototypes = "- type: Tag\n  id: {MyTag}";

                public ProtoId<TagPrototype> MyRef = "MyTagDummy";
            }
            """;

        await Verify(code, "Content.IntegrationTests",
            new DiagnosticResult("HONK0014", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithSpan("/0/Test1.cs", 11, 42, 11, 54)
                .WithArguments("MyTagDummy"));
    }

    [Test]
    public async Task TestProtoId_ConstString_DoesNotReport()
    {
        const string code = """
            using Robust.UnitTesting;

            public sealed class Foo
            {
                private const string MyTag = "MyTagDummy";

                [TestPrototypes]
                private const string Prototypes = "- type: Tag\n  id: {MyTag}";
            }
            """;

        await Verify(code, "Content.IntegrationTests");
    }

    [Test]
    public async Task ProductionAssembly_DoesNotReport()
    {
        const string code = """
            using Robust.Shared.Prototypes;
            using Robust.UnitTesting;

            public sealed class Foo
            {
                private const string MyTag = "MyTagDummy";

                [TestPrototypes]
                private const string Prototypes = "- type: Tag\n  id: {MyTag}";

                public ProtoId<TagPrototype> MyRef = "MyTagDummy";
            }
            """;

        await Verify(code, "Content.Shared");
    }

    [Test]
    public async Task UnrelatedProtoId_InTestAssembly_DoesNotReport()
    {
        const string code = """
            using Robust.Shared.Prototypes;
            using Robust.UnitTesting;

            public sealed class Foo
            {
                private const string MyTag = "MyTagDummy";

                [TestPrototypes]
                private const string Prototypes = "- type: Tag\n  id: {MyTag}";

                public ProtoId<TagPrototype> RealOne = "SomeRealTag";
            }
            """;

        await Verify(code, "Content.IntegrationTests");
    }

    [Test]
    public async Task LiteralIdInYaml_ProtoIdMatch_Reports()
    {
        const string code = """
            using Robust.Shared.Prototypes;
            using Robust.UnitTesting;

            public sealed class Foo
            {
                [TestPrototypes]
                private const string Prototypes = "- type: Tag\n  id: DirectDummy";

                public ProtoId<TagPrototype> MyRef = "DirectDummy";
            }
            """;

        await Verify(code, "Content.IntegrationTests",
            new DiagnosticResult("HONK0014", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithSpan("/0/Test1.cs", 9, 42, 9, 55)
                .WithArguments("DirectDummy"));
    }
}
