using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;

namespace Content.Analyzers.Honk.Tests;

using VerifyCS = CSharpCodeFixTest<HonkMagicNumberAnalyzer, HonkMagicNumberFixer, DefaultVerifier>;

[TestFixture]
public sealed class HonkMagicNumberFixerTest
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
    public async Task FloatLiteralOnField_ExtractsConst()
    {
        const string before = """
            using Robust.Shared.Serialization.Manager.Attributes;

            public sealed class ExampleComp
            {
                [DataField]
                public float Modifier = 0.5f;
            }
            """;

        const string after = """
            using Robust.Shared.Serialization.Manager.Attributes;

            public sealed class ExampleComp
            {
                private const float ModifierDefault = 0.5f;
                [DataField]
                public float Modifier = ModifierDefault;
            }
            """;

        await Verify(before, after,
            new DiagnosticResult("HONK0016", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithSpan(ForkPath, 6, 29, 6, 33)
                .WithArguments("Modifier", "0.5f"));
    }

    [Test]
    public async Task NegativeLiteralOnProperty_ExtractsConst()
    {
        const string before = """
            using Robust.Shared.Serialization.Manager.Attributes;

            public sealed class ExampleComp
            {
                [DataField]
                public float Volume { get; set; } = -6f;
            }
            """;

        const string after = """
            using Robust.Shared.Serialization.Manager.Attributes;

            public sealed class ExampleComp
            {
                private const float VolumeDefault = -6f;

                [DataField]
                public float Volume { get; set; } = VolumeDefault;
            }
            """;

        await Verify(before, after,
            new DiagnosticResult("HONK0016", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithSpan(ForkPath, 6, 41, 6, 44)
                .WithArguments("Volume", "-6f"));
    }

    [Test]
    public async Task IntLiteralOnField_ExtractsConst()
    {
        const string before = """
            using Robust.Shared.Serialization.Manager.Attributes;

            public sealed class ExampleComp
            {
                [DataField]
                public int MaxStack = 30;
            }
            """;

        const string after = """
            using Robust.Shared.Serialization.Manager.Attributes;

            public sealed class ExampleComp
            {
                private const int MaxStackDefault = 30;
                [DataField]
                public int MaxStack = MaxStackDefault;
            }
            """;

        await Verify(before, after,
            new DiagnosticResult("HONK0016", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithSpan(ForkPath, 6, 27, 6, 29)
                .WithArguments("MaxStack", "30"));
    }
}
