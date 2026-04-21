using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;

namespace Content.Analyzers.Honk.Tests;

using VerifyCS = CSharpAnalyzerTest<HonkMagicNumberAnalyzer, DefaultVerifier>;

[TestFixture]
public sealed class HonkMagicNumberAnalyzerTest
{
    private const string Stubs = """
        namespace Robust.Shared.Serialization.Manager.Attributes
        {
            [System.AttributeUsage(System.AttributeTargets.Field | System.AttributeTargets.Property)]
            public sealed class DataFieldAttribute : System.Attribute
            {
                public DataFieldAttribute() { }
                public DataFieldAttribute(string tag) { }
            }
        }
        """;

    private const string ForkPath = "Content.Shared/RussStation/Example/ExampleComp.cs";
    private const string HonkPath = "Content.Shared/Medical/HealthAnalyzerSystem.Honk.cs";
    private const string UpstreamPath = "Content.Shared/Upstream/UpstreamComp.cs";

    private static Task Verify(string code, string filePath, params DiagnosticResult[] expected)
    {
        var test = new VerifyCS { TestState = { Sources = { Stubs, (filePath, code) } } };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Test]
    public async Task NumericLiteralOnDataField_Reports()
    {
        const string code = """
            using Robust.Shared.Serialization.Manager.Attributes;

            public sealed class ExampleComp
            {
                [DataField]
                public float Modifier = 0.5f;
            }
            """;

        await Verify(code, ForkPath,
            new DiagnosticResult("HONK0016", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithSpan(ForkPath, 6, 29, 6, 33)
                .WithArguments("Modifier", "0.5f"));
    }

    [Test]
    public async Task NegativeNumericLiteralOnDataField_Reports()
    {
        const string code = """
            using Robust.Shared.Serialization.Manager.Attributes;

            public sealed class ExampleComp
            {
                [DataField]
                public float Volume = -6f;
            }
            """;

        await Verify(code, ForkPath,
            new DiagnosticResult("HONK0016", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithSpan(ForkPath, 6, 27, 6, 30)
                .WithArguments("Volume", "-6f"));
    }

    [Test]
    public async Task ZeroLiteralOnDataField_Reports()
    {
        // Even `0` / `1` count as magic numbers when they encode a default choice.
        const string code = """
            using Robust.Shared.Serialization.Manager.Attributes;

            public sealed class ExampleComp
            {
                [DataField]
                public int Balance = 0;
            }
            """;

        await Verify(code, ForkPath,
            new DiagnosticResult("HONK0016", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithSpan(ForkPath, 6, 26, 6, 27)
                .WithArguments("Balance", "0"));
    }

    [Test]
    public async Task LiteralOnDataFieldProperty_Reports()
    {
        const string code = """
            using Robust.Shared.Serialization.Manager.Attributes;

            public sealed class ExampleComp
            {
                [DataField]
                public float Modifier { get; set; } = 1.5f;
            }
            """;

        await Verify(code, ForkPath,
            new DiagnosticResult("HONK0016", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithSpan(ForkPath, 6, 43, 6, 47)
                .WithArguments("Modifier", "1.5f"));
    }

    [Test]
    public async Task ConstantReferenceOnDataField_DoesNotReport()
    {
        const string code = """
            using Robust.Shared.Serialization.Manager.Attributes;

            public static class ExampleConstants
            {
                public const float DefaultModifier = 0.5f;
            }

            public sealed class ExampleComp
            {
                [DataField]
                public float Modifier = ExampleConstants.DefaultModifier;
            }
            """;

        await Verify(code, ForkPath);
    }

    [Test]
    public async Task NoInitializer_DoesNotReport()
    {
        const string code = """
            using Robust.Shared.Serialization.Manager.Attributes;

            public sealed class ExampleComp
            {
                [DataField]
                public float Modifier;
            }
            """;

        await Verify(code, ForkPath);
    }

    [Test]
    public async Task StringLiteral_DoesNotReport()
    {
        const string code = """
            using Robust.Shared.Serialization.Manager.Attributes;

            public sealed class ExampleComp
            {
                [DataField]
                public string Id = "foo";
            }
            """;

        await Verify(code, ForkPath);
    }

    [Test]
    public async Task BoolLiteral_DoesNotReport()
    {
        const string code = """
            using Robust.Shared.Serialization.Manager.Attributes;

            public sealed class ExampleComp
            {
                [DataField]
                public bool Enabled = true;
            }
            """;

        await Verify(code, ForkPath);
    }

    [Test]
    public async Task NonDataFieldNumericLiteral_DoesNotReport()
    {
        const string code = """
            public sealed class ExampleComp
            {
                public float Modifier = 0.5f;
            }
            """;

        await Verify(code, ForkPath);
    }

    [Test]
    public async Task UpstreamFileNumericLiteral_DoesNotReport()
    {
        const string code = """
            using Robust.Shared.Serialization.Manager.Attributes;

            public sealed class UpstreamComp
            {
                [DataField]
                public float Modifier = 0.5f;
            }
            """;

        await Verify(code, UpstreamPath);
    }

    [Test]
    public async Task HonkSuffixFile_Reports()
    {
        const string code = """
            using Robust.Shared.Serialization.Manager.Attributes;

            public sealed class HonkComp
            {
                [DataField]
                public float Modifier = 0.5f;
            }
            """;

        await Verify(code, HonkPath,
            new DiagnosticResult("HONK0016", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithSpan(HonkPath, 6, 29, 6, 33)
                .WithArguments("Modifier", "0.5f"));
    }

    [Test]
    public async Task QualifiedAttributeName_Reports()
    {
        const string code = """
            public sealed class ExampleComp
            {
                [Robust.Shared.Serialization.Manager.Attributes.DataField]
                public float Modifier = 0.5f;
            }
            """;

        await Verify(code, ForkPath,
            new DiagnosticResult("HONK0016", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithSpan(ForkPath, 4, 29, 4, 33)
                .WithArguments("Modifier", "0.5f"));
    }

    [Test]
    public async Task CollectionExpressionElements_Report()
    {
        const string code = """
            using Robust.Shared.Serialization.Manager.Attributes;

            public sealed class ExampleComp
            {
                [DataField]
                public float[] Thresholds = [1f, 3f, 6f];
            }
            """;

        await Verify(code, ForkPath,
            new DiagnosticResult("HONK0016", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithSpan(ForkPath, 6, 34, 6, 36)
                .WithArguments("Thresholds", "1f"),
            new DiagnosticResult("HONK0016", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithSpan(ForkPath, 6, 38, 6, 40)
                .WithArguments("Thresholds", "3f"),
            new DiagnosticResult("HONK0016", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithSpan(ForkPath, 6, 42, 6, 44)
                .WithArguments("Thresholds", "6f"));
    }

    [Test]
    public async Task ExplicitArrayCreationElements_Report()
    {
        const string code = """
            using Robust.Shared.Serialization.Manager.Attributes;

            public sealed class ExampleComp
            {
                [DataField]
                public float[] Thresholds = new float[] { 1f, 3f, 6f };
            }
            """;

        await Verify(code, ForkPath,
            new DiagnosticResult("HONK0016", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithSpan(ForkPath, 6, 47, 6, 49)
                .WithArguments("Thresholds", "1f"),
            new DiagnosticResult("HONK0016", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithSpan(ForkPath, 6, 51, 6, 53)
                .WithArguments("Thresholds", "3f"),
            new DiagnosticResult("HONK0016", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithSpan(ForkPath, 6, 55, 6, 57)
                .WithArguments("Thresholds", "6f"));
    }

    [Test]
    public async Task ImplicitArrayCreationElements_Report()
    {
        const string code = """
            using Robust.Shared.Serialization.Manager.Attributes;

            public sealed class ExampleComp
            {
                [DataField]
                public float[] Thresholds = new[] { 1f, 3f, 6f };
            }
            """;

        await Verify(code, ForkPath,
            new DiagnosticResult("HONK0016", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithSpan(ForkPath, 6, 41, 6, 43)
                .WithArguments("Thresholds", "1f"),
            new DiagnosticResult("HONK0016", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithSpan(ForkPath, 6, 45, 6, 47)
                .WithArguments("Thresholds", "3f"),
            new DiagnosticResult("HONK0016", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithSpan(ForkPath, 6, 49, 6, 51)
                .WithArguments("Thresholds", "6f"));
    }

    [Test]
    public async Task CollectionExpressionOfConstantReferences_DoesNotReport()
    {
        const string code = """
            using Robust.Shared.Serialization.Manager.Attributes;

            public static class ExampleConstants
            {
                public const float T1 = 1f;
                public const float T2 = 3f;
                public const float T3 = 6f;
            }

            public sealed class ExampleComp
            {
                [DataField]
                public float[] Thresholds = [ExampleConstants.T1, ExampleConstants.T2, ExampleConstants.T3];
            }
            """;

        await Verify(code, ForkPath);
    }
}
