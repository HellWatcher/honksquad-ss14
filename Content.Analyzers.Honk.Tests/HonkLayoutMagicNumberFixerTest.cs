using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;

namespace Content.Analyzers.Honk.Tests;

using VerifyCS = CSharpCodeFixTest<HonkLayoutMagicNumberAnalyzer, HonkLayoutMagicNumberFixer, DefaultVerifier>;

[TestFixture]
public sealed class HonkLayoutMagicNumberFixerTest
{
    private const string Stubs = """
        namespace Robust.Shared.Maths
        {
            public readonly struct Thickness
            {
                public Thickness(float uniform) { }
                public Thickness(float horizontal, float vertical) { }
                public Thickness(float left, float top, float right, float bottom) { }
            }
        }
        namespace System.Numerics
        {
            public readonly struct Vector2
            {
                public Vector2(float x, float y) { }
            }
        }
        """;

    private const string ForkPath = "Content.Shared/RussStation/Example/Widget.cs";

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
    public async Task SingleLiteralFieldInitializer_ExtractsConst()
    {
        const string before = """
            using Robust.Shared.Maths;

            public sealed class Widget
            {
                public Thickness Margin = new Thickness(4f);
            }
            """;

        const string after = """
            using Robust.Shared.Maths;

            public sealed class Widget
            {
                private const float MarginDefault = 4f;
                public Thickness Margin = new Thickness(MarginDefault);
            }
            """;

        await Verify(before, after,
            new DiagnosticResult("HONK0017", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithSpan(ForkPath, 5, 45, 5, 47)
                .WithArguments("Thickness", "4f", 1));
    }

    [Test]
    public async Task SingleLiteralTimeSpanInitializer_ExtractsConst()
    {
        const string before = """
            using System;

            public sealed class Widget
            {
                public TimeSpan Cooldown = TimeSpan.FromSeconds(2.5);
            }
            """;

        const string after = """
            using System;

            public sealed class Widget
            {
                private const double CooldownDefault = 2.5;
                public TimeSpan Cooldown = TimeSpan.FromSeconds(CooldownDefault);
            }
            """;

        await Verify(before, after,
            new DiagnosticResult("HONK0017", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithSpan(ForkPath, 5, 53, 5, 56)
                .WithArguments("TimeSpan.FromSeconds", "2.5", 1));
    }

    [Test]
    public async Task MultipleLiteralsInSameCtor_NoFixOffered()
    {
        const string code = """
            using System.Numerics;

            public sealed class Widget
            {
                public Vector2 Offset = new Vector2(3f, 5f);
            }
            """;

        var test = new VerifyCS
        {
            TestState = { Sources = { Stubs, (ForkPath, code) } },
            // Fix shouldn't engage on multi-literal ctors, so the code stays the same.
            FixedState = { Sources = { Stubs, (ForkPath, code) }, MarkupHandling = MarkupMode.Allow },
        };
        test.ExpectedDiagnostics.Add(new DiagnosticResult("HONK0017", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
            .WithSpan(ForkPath, 5, 41, 5, 43)
            .WithArguments("Vector2", "3f", 1));
        test.ExpectedDiagnostics.Add(new DiagnosticResult("HONK0017", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
            .WithSpan(ForkPath, 5, 45, 5, 47)
            .WithArguments("Vector2", "5f", 2));
        // Both diagnostics remain in the fixed state: the fixer declines to engage.
        test.FixedState.ExpectedDiagnostics.Add(new DiagnosticResult("HONK0017", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
            .WithSpan(ForkPath, 5, 41, 5, 43)
            .WithArguments("Vector2", "3f", 1));
        test.FixedState.ExpectedDiagnostics.Add(new DiagnosticResult("HONK0017", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
            .WithSpan(ForkPath, 5, 45, 5, 47)
            .WithArguments("Vector2", "5f", 2));
        test.NumberOfFixAllIterations = 0;
        test.NumberOfIncrementalIterations = 0;
        await test.RunAsync();
    }
}
