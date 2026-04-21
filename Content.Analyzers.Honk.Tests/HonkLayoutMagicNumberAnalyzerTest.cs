using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;

namespace Content.Analyzers.Honk.Tests;

using VerifyCS = CSharpAnalyzerTest<HonkLayoutMagicNumberAnalyzer, DefaultVerifier>;

[TestFixture]
public sealed class HonkLayoutMagicNumberAnalyzerTest
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
            public readonly struct UIBox2
            {
                public UIBox2(float left, float top, float right, float bottom) { }
            }
            public readonly struct Color
            {
                public Color(float r, float g, float b, float a) { }
            }
        }
        namespace System.Numerics
        {
            public readonly struct Vector2
            {
                public Vector2(float x, float y) { }
            }
            public readonly struct Vector3
            {
                public Vector3(float x, float y, float z) { }
            }
        }
        """;

    private const string ForkPath = "Content.Shared/RussStation/Example/Example.cs";
    private const string UpstreamPath = "Content.Shared/Upstream/Upstream.cs";

    private static Task Verify(string code, string filePath, params DiagnosticResult[] expected)
    {
        var test = new VerifyCS { TestState = { Sources = { Stubs, (filePath, code) } } };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Test]
    public async Task ThicknessWithNumericArgs_ReportsPerArgument()
    {
        const string code = """
            using Robust.Shared.Maths;

            public sealed class Widget
            {
                public void Build()
                {
                    var margin = new Thickness(0, 8, 0, 0);
                }
            }
            """;

        await Verify(code, ForkPath,
            new DiagnosticResult("HONK0017", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithSpan(ForkPath, 7, 40, 7, 41)
                .WithArguments("Thickness", "0", 1),
            new DiagnosticResult("HONK0017", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithSpan(ForkPath, 7, 43, 7, 44)
                .WithArguments("Thickness", "8", 2),
            new DiagnosticResult("HONK0017", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithSpan(ForkPath, 7, 46, 7, 47)
                .WithArguments("Thickness", "0", 3),
            new DiagnosticResult("HONK0017", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithSpan(ForkPath, 7, 49, 7, 50)
                .WithArguments("Thickness", "0", 4));
    }

    [Test]
    public async Task Vector2WithNumericArgs_Reports()
    {
        const string code = """
            using System.Numerics;

            public sealed class Widget
            {
                public void Build()
                {
                    var pos = new Vector2(16f, 16f);
                }
            }
            """;

        await Verify(code, ForkPath,
            new DiagnosticResult("HONK0017", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithSpan(ForkPath, 7, 35, 7, 38)
                .WithArguments("Vector2", "16f", 1),
            new DiagnosticResult("HONK0017", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithSpan(ForkPath, 7, 40, 7, 43)
                .WithArguments("Vector2", "16f", 2));
    }

    [Test]
    public async Task TimeSpanFromSecondsWithNumericLiteral_Reports()
    {
        const string code = """
            using System;

            public sealed class Widget
            {
                public void Build()
                {
                    var delay = TimeSpan.FromSeconds(3);
                }
            }
            """;

        await Verify(code, ForkPath,
            new DiagnosticResult("HONK0017", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithSpan(ForkPath, 7, 42, 7, 43)
                .WithArguments("TimeSpan.FromSeconds", "3", 1));
    }

    [Test]
    public async Task ThicknessWithNamedConstants_DoesNotReport()
    {
        const string code = """
            using Robust.Shared.Maths;

            public static class Layout
            {
                public const int TopMargin = 8;
                public const int ZeroMargin = 0;
            }

            public sealed class Widget
            {
                public void Build()
                {
                    var margin = new Thickness(Layout.ZeroMargin, Layout.TopMargin, Layout.ZeroMargin, Layout.ZeroMargin);
                }
            }
            """;

        await Verify(code, ForkPath);
    }

    [Test]
    public async Task TimeSpanInsideStaticReadonly_DoesNotReport()
    {
        // The constants file itself is where TimeSpan.FromSeconds(N) IS the named value.
        const string code = """
            using System;

            public static class LayoutConstants
            {
                public static readonly TimeSpan Interval = TimeSpan.FromSeconds(3);
            }
            """;

        await Verify(code, ForkPath);
    }

    [Test]
    public async Task ThicknessInsideConstField_DoesNotReport()
    {
        // `const` field — this is the defining site, so literals are allowed.
        const string code = """
            using Robust.Shared.Maths;

            public static class LayoutConstants
            {
                public const int SideMargin = 8;
            }
            """;

        await Verify(code, ForkPath);
    }

    [Test]
    public async Task UpstreamFileLiteralArgs_DoesNotReport()
    {
        const string code = """
            using Robust.Shared.Maths;

            public sealed class Widget
            {
                public void Build()
                {
                    var margin = new Thickness(0, 8, 0, 0);
                    var delay = System.TimeSpan.FromSeconds(3);
                }
            }
            """;

        await Verify(code, UpstreamPath);
    }

    [Test]
    public async Task QualifiedTypeName_Reports()
    {
        // Matches by rightmost identifier so `Robust.Shared.Maths.Thickness` still fires.
        const string code = """
            public sealed class Widget
            {
                public void Build()
                {
                    var margin = new Robust.Shared.Maths.Thickness(0, 8, 0, 0);
                }
            }
            """;

        await Verify(code, ForkPath,
            new DiagnosticResult("HONK0017", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithSpan(ForkPath, 5, 56, 5, 57)
                .WithArguments("Thickness", "0", 1),
            new DiagnosticResult("HONK0017", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithSpan(ForkPath, 5, 59, 5, 60)
                .WithArguments("Thickness", "8", 2),
            new DiagnosticResult("HONK0017", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithSpan(ForkPath, 5, 62, 5, 63)
                .WithArguments("Thickness", "0", 3),
            new DiagnosticResult("HONK0017", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithSpan(ForkPath, 5, 65, 5, 66)
                .WithArguments("Thickness", "0", 4));
    }

    [Test]
    public async Task UnrelatedTypeName_DoesNotReport()
    {
        // `Thickness` is the only heuristic — other types with numeric ctors are out of scope.
        const string code = """
            public readonly struct SomeOtherType
            {
                public SomeOtherType(int a, int b) { }
            }

            public sealed class Widget
            {
                public void Build()
                {
                    var x = new SomeOtherType(1, 2);
                }
            }
            """;

        await Verify(code, ForkPath);
    }

    [Test]
    public async Task TimeSpanFromTicksInsideMethod_Reports()
    {
        const string code = """
            using System;

            public sealed class Widget
            {
                public TimeSpan Delay() => TimeSpan.FromTicks(100);
            }
            """;

        await Verify(code, ForkPath,
            new DiagnosticResult("HONK0017", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithSpan(ForkPath, 5, 51, 5, 54)
                .WithArguments("TimeSpan.FromTicks", "100", 1));
    }

    [Test]
    public async Task TimeSpanFromSecondsWithExpression_DoesNotReport()
    {
        // Not a raw literal — variable, computation, constant reference all pass.
        const string code = """
            using System;

            public sealed class Widget
            {
                public TimeSpan Delay(int seconds) => TimeSpan.FromSeconds(seconds);
            }
            """;

        await Verify(code, ForkPath);
    }

    [Test]
    public async Task NonTimeSpanFromSecondsLookalike_DoesNotReport()
    {
        // Only `TimeSpan.From*` is in scope; same method name on a different type passes.
        const string code = """
            public static class NotTimeSpan
            {
                public static int FromSeconds(int n) => n;
            }

            public sealed class Widget
            {
                public int Delay() => NotTimeSpan.FromSeconds(3);
            }
            """;

        await Verify(code, ForkPath);
    }
}
