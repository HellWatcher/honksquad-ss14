using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;

namespace Content.Analyzers.Honk.Tests;

using VerifyCS = CSharpAnalyzerTest<HonkYamlMappingChildrenSandboxAnalyzer, DefaultVerifier>;

[TestFixture]
public sealed class HonkYamlMappingChildrenSandboxAnalyzerTest
{
    private const string Stubs = """
        namespace YamlDotNet.RepresentationModel
        {
            public class YamlNode { }
            public sealed class YamlMappingNode : YamlNode
            {
                public System.Collections.Generic.IDictionary<YamlNode, YamlNode> Children
                    => new System.Collections.Generic.Dictionary<YamlNode, YamlNode>();
            }
        }
        public sealed class OtherChildren
        {
            public int Children => 0;
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
    public async Task ChildrenAccess_InSharedForkFile_Reports()
    {
        const string code = """
            using YamlDotNet.RepresentationModel;

            public static class T
            {
                public static int Run(YamlMappingNode node) => node.Children.Count;
            }
            """;

        await Verify(code, "Content.Shared/RussStation/Foo/FooSystem.cs",
            new DiagnosticResult("HONK0021", DiagnosticSeverity.Warning)
                .WithSpan("Content.Shared/RussStation/Foo/FooSystem.cs", 5, 57, 5, 65));
    }

    [Test]
    public async Task ChildrenAccess_InServer_DoesNotReport()
    {
        const string code = """
            using YamlDotNet.RepresentationModel;

            public static class T
            {
                public static int Run(YamlMappingNode node) => node.Children.Count;
            }
            """;

        await Verify(code, "Content.Server/RussStation/Foo/FooSystem.cs");
    }

    [Test]
    public async Task ChildrenAccess_OnUnrelatedType_DoesNotReport()
    {
        const string code = """
            public static class T
            {
                public static int Run(OtherChildren x) => x.Children;
            }
            """;

        await Verify(code, "Content.Shared/RussStation/Foo/FooSystem.cs");
    }

    [Test]
    public async Task ChildrenAccess_InUpstreamShared_DoesNotReport()
    {
        const string code = """
            using YamlDotNet.RepresentationModel;

            public static class T
            {
                public static int Run(YamlMappingNode node) => node.Children.Count;
            }
            """;

        await Verify(code, "Content.Shared/Foo/FooSystem.cs");
    }

    [Test]
    public async Task ChildrenAccess_InHonkPartialShared_Reports()
    {
        const string code = """
            using YamlDotNet.RepresentationModel;

            public static class T
            {
                public static int Run(YamlMappingNode node) => node.Children.Count;
            }
            """;

        await Verify(code, "Content.Shared/Foo/FooSystem.Honk.cs",
            new DiagnosticResult("HONK0021", DiagnosticSeverity.Warning)
                .WithSpan("Content.Shared/Foo/FooSystem.Honk.cs", 5, 57, 5, 65));
    }
}
