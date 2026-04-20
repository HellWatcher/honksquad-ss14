using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;

namespace Content.Analyzers.Honk.Tests;

using VerifyCS = CSharpAnalyzerTest<HonkPartialProliferationAnalyzer, DefaultVerifier>;

[TestFixture]
public sealed class HonkPartialProliferationAnalyzerTest
{
    private const string Base = """
        namespace Content.Shared.Upstream
        {
            public partial class Target { }
            public partial class OtherTarget { }
        }
        """;

    private static Task Verify(params (string Path, string Code)[] files)
    {
        var test = new VerifyCS();
        test.TestState.Sources.Add(Base);
        foreach (var (path, code) in files)
            test.TestState.Sources.Add((path, code));
        return test.RunAsync();
    }

    private static Task VerifyWith(DiagnosticResult[] expected, params (string Path, string Code)[] files)
    {
        var test = new VerifyCS();
        test.TestState.Sources.Add(Base);
        foreach (var (path, code) in files)
            test.TestState.Sources.Add((path, code));
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    private const string PartialA = """
        namespace Content.Shared.Upstream;
        public partial class Target { public int A; }
        """;

    private const string PartialB = """
        namespace Content.Shared.Upstream;
        public partial class Target { public int B; }
        """;

    private const string PartialC = """
        namespace Content.Shared.Upstream;
        public partial class Target { public int C; }
        """;

    private const string PartialD = """
        namespace Content.Shared.Upstream;
        public partial class Target { public int D; }
        """;

    [Test]
    public async Task TwoHonkPartials_DoNotReport()
    {
        await Verify(
            ("Content.Shared/Upstream/Target.A.Honk.cs", PartialA),
            ("Content.Shared/Upstream/Target.B.Honk.cs", PartialB));
    }

    [Test]
    public async Task ThreeHonkPartials_ReportThird()
    {
        const string third = "Content.Shared/Upstream/Target.C.Honk.cs";

        await VerifyWith(
            new[]
            {
                new DiagnosticResult("HONK0015", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                    .WithSpan(third, 2, 22, 2, 28)
                    .WithArguments("global::Content.Shared.Upstream.Target", 3),
            },
            ("Content.Shared/Upstream/Target.A.Honk.cs", PartialA),
            ("Content.Shared/Upstream/Target.B.Honk.cs", PartialB),
            (third, PartialC));
    }

    [Test]
    public async Task FourHonkPartials_ReportThirdAndFourth()
    {
        const string third = "Content.Shared/Upstream/Target.C.Honk.cs";
        const string fourth = "Content.Shared/Upstream/Target.D.Honk.cs";

        await VerifyWith(
            new[]
            {
                new DiagnosticResult("HONK0015", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                    .WithSpan(third, 2, 22, 2, 28)
                    .WithArguments("global::Content.Shared.Upstream.Target", 3),
                new DiagnosticResult("HONK0015", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                    .WithSpan(fourth, 2, 22, 2, 28)
                    .WithArguments("global::Content.Shared.Upstream.Target", 4),
            },
            ("Content.Shared/Upstream/Target.A.Honk.cs", PartialA),
            ("Content.Shared/Upstream/Target.B.Honk.cs", PartialB),
            (third, PartialC),
            (fourth, PartialD));
    }

    [Test]
    public async Task DifferentTargets_DoNotCrossContaminate()
    {
        const string otherA = """
            namespace Content.Shared.Upstream;
            public partial class OtherTarget { public int A; }
            """;
        const string otherB = """
            namespace Content.Shared.Upstream;
            public partial class OtherTarget { public int B; }
            """;

        await Verify(
            ("Content.Shared/Upstream/Target.A.Honk.cs", PartialA),
            ("Content.Shared/Upstream/Target.B.Honk.cs", PartialB),
            ("Content.Shared/Upstream/OtherTarget.A.Honk.cs", otherA),
            ("Content.Shared/Upstream/OtherTarget.B.Honk.cs", otherB));
    }

    [Test]
    public async Task NonHonkPartials_DoNotCount()
    {
        await Verify(
            ("Content.Shared/Upstream/Target.A.Honk.cs", PartialA),
            ("Content.Shared/Upstream/Target.B.Honk.cs", PartialB),
            ("Content.Shared/Upstream/Target.Plain.cs", PartialC));
    }
}
