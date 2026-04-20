using System.Threading.Tasks;
using Content.Analyzers.Honk;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;

namespace Content.Analyzers.Honk.Tests;

using VerifyCS = CSharpAnalyzerTest<HonkUpstreamFieldWriteAnalyzer, DefaultVerifier>;

[TestFixture]
public sealed class HonkUpstreamFieldWriteAnalyzerTest
{
    private const string Stubs = """
        namespace Content.Shared.Body.Components
        {
            public sealed class BloodstreamComponent
            {
                public float BloodLevel;
                public int MaxBlood { get; set; }
            }
        }
        namespace Content.Shared.RussStation.Wounds
        {
            public sealed class WoundComponent
            {
                public string? BleedSourceDamageType;
            }
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
    public async Task WriteUpstreamField_FromForkFile_Reports()
    {
        const string code = """
            using Content.Shared.Body.Components;

            public static class T
            {
                public static void Run(BloodstreamComponent comp)
                {
                    comp.BloodLevel = 1.0f;
                }
            }
            """;

        await Verify(code, "Content.Shared/RussStation/Traits/BloodDeficiencySystem.cs",
            new DiagnosticResult("HONK0003", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithSpan("Content.Shared/RussStation/Traits/BloodDeficiencySystem.cs", 7, 14, 7, 24)
                .WithArguments("BloodstreamComponent", "BloodLevel"));
    }

    [Test]
    public async Task WriteUpstreamProperty_FromForkFile_Reports()
    {
        const string code = """
            using Content.Shared.Body.Components;

            public static class T
            {
                public static void Run(BloodstreamComponent comp)
                {
                    comp.MaxBlood = 500;
                }
            }
            """;

        await Verify(code, "Content.Shared/RussStation/Traits/BloodDeficiencySystem.cs",
            new DiagnosticResult("HONK0003", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithSpan("Content.Shared/RussStation/Traits/BloodDeficiencySystem.cs", 7, 14, 7, 22)
                .WithArguments("BloodstreamComponent", "MaxBlood"));
    }

    [Test]
    public async Task WriteForkComponent_FromForkFile_DoesNotReport()
    {
        const string code = """
            using Content.Shared.RussStation.Wounds;

            public static class T
            {
                public static void Run(WoundComponent comp)
                {
                    comp.BleedSourceDamageType = "Slash";
                }
            }
            """;

        await Verify(code, "Content.Shared/RussStation/Wounds/Systems/WoundDisplaySystem.cs");
    }

    [Test]
    public async Task WriteUpstreamField_FromUpstreamFile_DoesNotReport()
    {
        const string code = """
            using Content.Shared.Body.Components;

            public static class T
            {
                public static void Run(BloodstreamComponent comp)
                {
                    comp.BloodLevel = 1.0f;
                }
            }
            """;

        await Verify(code, "Content.Shared/Body/Systems/SharedBloodstreamSystem.cs");
    }

    [Test]
    public async Task WriteUpstreamField_FromHonkPartial_DoesNotReport()
    {
        const string code = """
            using Content.Shared.Body.Components;

            public static class T
            {
                public static void Run(BloodstreamComponent comp)
                {
                    comp.BloodLevel = 1.0f;
                }
            }
            """;

        await Verify(code, "Content.Shared/RussStation/Traits/BloodDeficiencySystem.Honk.cs");
    }
}
