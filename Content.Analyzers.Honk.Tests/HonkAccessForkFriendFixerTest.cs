using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;

namespace Content.Analyzers.Honk.Tests;

using VerifyCS = CSharpCodeFixTest<HonkAccessForkFriendAnalyzer, HonkAccessForkFriendFixer, DefaultVerifier>;

[TestFixture]
public sealed class HonkAccessForkFriendFixerTest
{
    private const string Stubs = """
        namespace Robust.Shared.Analyzers
        {
            [System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = true)]
            public sealed class AccessAttribute : System.Attribute
            {
                public AccessAttribute() { }
                public AccessAttribute(System.Type type) { }
                public AccessAttribute(System.Type a, System.Type b) { }
            }
        }
        namespace Content.Shared.RussStation.Traits
        {
            public sealed class BloodDeficiencySystem { }
        }
        namespace Content.Shared.Body.Systems
        {
            public sealed class SharedBloodstreamSystem { }
        }
        """;

    private const string UpstreamPath = "Content.Shared/Body/Components/BloodstreamComponent.cs";

    private static Task Verify(string before, string after, params DiagnosticResult[] expected)
    {
        var test = new VerifyCS
        {
            TestState = { Sources = { Stubs, (UpstreamPath, before) } },
            FixedState = { Sources = { Stubs, (UpstreamPath, after) } },
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Test]
    public async Task TopLevelAttribute_WrapsWithMarkers()
    {
        const string before = """
            using Robust.Shared.Analyzers;
            using Content.Shared.RussStation.Traits;

            [Access(typeof(BloodDeficiencySystem))]
            public sealed class BloodstreamComponent { }
            """;

        const string after = """
            using Robust.Shared.Analyzers;
            using Content.Shared.RussStation.Traits;

            // HONK START
            [Access(typeof(BloodDeficiencySystem))]
            // HONK END
            public sealed class BloodstreamComponent { }
            """;

        await Verify(before, after,
            new DiagnosticResult("HONK0005", Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                .WithSpan(UpstreamPath, 4, 2, 4, 39)
                .WithArguments("BloodDeficiencySystem"));
    }

    [Test]
    public async Task IndentedAttribute_PreservesIndent()
    {
        const string before = """
            using Robust.Shared.Analyzers;
            using Content.Shared.RussStation.Traits;

            public static class Outer
            {
                [Access(typeof(BloodDeficiencySystem))]
                public sealed class BloodstreamComponent { }
            }
            """;

        const string after = """
            using Robust.Shared.Analyzers;
            using Content.Shared.RussStation.Traits;

            public static class Outer
            {
                // HONK START
                [Access(typeof(BloodDeficiencySystem))]
                // HONK END
                public sealed class BloodstreamComponent { }
            }
            """;

        await Verify(before, after,
            new DiagnosticResult("HONK0005", Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                .WithSpan(UpstreamPath, 6, 6, 6, 43)
                .WithArguments("BloodDeficiencySystem"));
    }

    [Test]
    public async Task CombinedAttributeList_NoFixOffered()
    {
        // [A, B] is a single list with two attributes; wrapping the whole list
        // would also wrap the unrelated upstream attribute, so the fixer bails.
        const string code = """
            using Robust.Shared.Analyzers;
            using Content.Shared.RussStation.Traits;
            using Content.Shared.Body.Systems;

            [Access(typeof(SharedBloodstreamSystem)), Access(typeof(BloodDeficiencySystem))]
            public sealed class BloodstreamComponent { }
            """;

        var test = new VerifyCS
        {
            TestState = { Sources = { Stubs, (UpstreamPath, code) } },
            FixedState = { Sources = { Stubs, (UpstreamPath, code) }, MarkupHandling = MarkupMode.Allow },
        };
        test.ExpectedDiagnostics.Add(new DiagnosticResult("HONK0005", Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
            .WithSpan(UpstreamPath, 5, 43, 5, 80)
            .WithArguments("BloodDeficiencySystem"));
        test.FixedState.ExpectedDiagnostics.Add(new DiagnosticResult("HONK0005", Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
            .WithSpan(UpstreamPath, 5, 43, 5, 80)
            .WithArguments("BloodDeficiencySystem"));
        test.NumberOfFixAllIterations = 0;
        test.NumberOfIncrementalIterations = 0;
        await test.RunAsync();
    }
}
