using System.Threading.Tasks;
using Content.Analyzers.Honk;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;

namespace Content.Analyzers.Honk.Tests;

using VerifyCS = CSharpAnalyzerTest<HonkAccessForkFriendAnalyzer, DefaultVerifier>;

[TestFixture]
public sealed class HonkAccessForkFriendAnalyzerTest
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

    private static Task Verify(string code, string filePath, params DiagnosticResult[] expected)
    {
        var test = new VerifyCS
        {
            TestState =
            {
                Sources =
                {
                    Stubs,
                    (filePath, code),
                },
            },
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Test]
    public async Task ForkFriend_InUpstreamFile_OutsideHonkBlock_Reports()
    {
        const string code = """
            using Robust.Shared.Analyzers;
            using Content.Shared.RussStation.Traits;

            [Access(typeof(BloodDeficiencySystem))]
            public sealed class BloodstreamComponent { }
            """;

        await Verify(code, "Content.Shared/Body/Components/BloodstreamComponent.cs",
            new DiagnosticResult("HONK0005", Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                .WithSpan("Content.Shared/Body/Components/BloodstreamComponent.cs", 4, 2, 4, 39)
                .WithArguments("BloodDeficiencySystem"));
    }

    [Test]
    public async Task ForkFriend_InUpstreamFile_InsideHonkBlock_DoesNotReport()
    {
        const string code = """
            using Robust.Shared.Analyzers;
            using Content.Shared.RussStation.Traits;
            using Content.Shared.Body.Systems;

            // HONK START
            [Access(typeof(SharedBloodstreamSystem), typeof(BloodDeficiencySystem))]
            // HONK END
            public sealed class BloodstreamComponent { }
            """;

        await Verify(code, "Content.Shared/Body/Components/BloodstreamComponent.cs");
    }

    [Test]
    public async Task UpstreamFriend_InUpstreamFile_OutsideHonkBlock_DoesNotReport()
    {
        const string code = """
            using Robust.Shared.Analyzers;
            using Content.Shared.Body.Systems;

            [Access(typeof(SharedBloodstreamSystem))]
            public sealed class BloodstreamComponent { }
            """;

        await Verify(code, "Content.Shared/Body/Components/BloodstreamComponent.cs");
    }

    [Test]
    public async Task ForkFriend_InForkFile_OutsideHonkBlock_DoesNotReport()
    {
        const string code = """
            using Robust.Shared.Analyzers;
            using Content.Shared.RussStation.Traits;

            [Access(typeof(BloodDeficiencySystem))]
            public sealed class SomeForkComponent { }
            """;

        await Verify(code, "Content.Shared/RussStation/Traits/SomeForkComponent.cs");
    }

    [Test]
    public async Task ForkFriend_InHonkPartialFile_OutsideHonkBlock_DoesNotReport()
    {
        const string code = """
            using Robust.Shared.Analyzers;
            using Content.Shared.RussStation.Traits;

            [Access(typeof(BloodDeficiencySystem))]
            public sealed class BloodstreamComponent { }
            """;

        await Verify(code, "Content.Shared/Body/Components/BloodstreamComponent.Honk.cs");
    }
}
