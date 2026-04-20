using System.Threading.Tasks;
using Content.Analyzers.Honk;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;

namespace Content.Analyzers.Honk.Tests;

using VerifyCS = CSharpAnalyzerTest<HonkPartialCounterpartAnalyzer, DefaultVerifier>;

[TestFixture]
public sealed class HonkPartialCounterpartAnalyzerTest
{
    [Test]
    public async Task HonkPartial_WithCounterpart_DoesNotReport()
    {
        var test = new VerifyCS
        {
            TestState =
            {
                Sources =
                {
                    ("Content.Shared/Foo/Bar.cs", """
                        namespace Foo;
                        public partial class Bar { }
                        """),
                    ("Content.Shared/Foo/Bar.Honk.cs", """
                        namespace Foo;
                        public partial class Bar { }
                        """),
                },
            },
        };
        await test.RunAsync();
    }

    [Test]
    public async Task HonkPartial_MissingCounterpart_Reports()
    {
        var test = new VerifyCS
        {
            TestState =
            {
                Sources =
                {
                    ("Content.Shared/Foo/Bar.Honk.cs", """
                        namespace Foo;
                        public partial class Bar { }
                        """),
                },
                ExpectedDiagnostics =
                {
                    new DiagnosticResult("HONK0006", Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                        .WithSpan("Content.Shared/Foo/Bar.Honk.cs", 2, 22, 2, 25)
                        .WithArguments("Bar", "has no non-Honk counterpart declaration"),
                },
            },
        };
        await test.RunAsync();
    }

    [Test]
    public async Task HonkFile_NonPartialClass_Reports()
    {
        var test = new VerifyCS
        {
            TestState =
            {
                Sources =
                {
                    ("Content.Shared/Foo/Bar.Honk.cs", """
                        namespace Foo;
                        public class Bar { }
                        """),
                },
                ExpectedDiagnostics =
                {
                    new DiagnosticResult("HONK0006", Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                        .WithSpan("Content.Shared/Foo/Bar.Honk.cs", 2, 14, 2, 17)
                        .WithArguments("Bar", "is not declared partial"),
                },
            },
        };
        await test.RunAsync();
    }

    [Test]
    public async Task NonHonkFile_NonPartialClass_DoesNotReport()
    {
        var test = new VerifyCS
        {
            TestState =
            {
                Sources =
                {
                    ("Content.Shared/Foo/Bar.cs", """
                        namespace Foo;
                        public class Bar { }
                        """),
                },
            },
        };
        await test.RunAsync();
    }
}
