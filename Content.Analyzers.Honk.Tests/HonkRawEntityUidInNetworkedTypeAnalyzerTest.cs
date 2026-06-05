using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;

namespace Content.Analyzers.Honk.Tests;

using VerifyCS = CSharpAnalyzerTest<HonkRawEntityUidInNetworkedTypeAnalyzer, DefaultVerifier>;

[TestFixture]
public sealed class HonkRawEntityUidInNetworkedTypeAnalyzerTest
{
    private const string Stubs = """
        namespace Robust.Shared.GameObjects
        {
            public readonly struct EntityUid { }
        }
        namespace Robust.Shared.Serialization
        {
            public readonly struct NetEntity { }

            [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct | System.AttributeTargets.Enum)]
            public sealed class NetSerializableAttribute : System.Attribute { }
        }
        namespace Robust.Shared.GameObjects
        {
            // EntityEventArgs is itself [NetSerializable] in Robust, so a local
            // event whose own type is NOT marked must still go unflagged.
            [Robust.Shared.Serialization.NetSerializable]
            public abstract class EntityEventArgs { }
            public abstract class BoundUserInterfaceState { }
        }
        namespace Robust.Shared.Analyzers
        {
            [System.AttributeUsage(System.AttributeTargets.Field | System.AttributeTargets.Property)]
            public sealed class AutoNetworkedFieldAttribute : System.Attribute { }
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
    public async Task NetSerializableStruct_EntityUidField_ForkFile_Reports()
    {
        const string code = """
            using Robust.Shared.GameObjects;
            using Robust.Shared.Serialization;

            [NetSerializable]
            public struct FooState
            {
                public EntityUid Target;
            }
            """;

        await Verify(code, "Content.Shared/RussStation/Foo/FooState.cs",
            new DiagnosticResult("HONK0025", DiagnosticSeverity.Warning)
                .WithSpan("Content.Shared/RussStation/Foo/FooState.cs", 7, 22, 7, 28)
                .WithArguments("Target"));
    }

    [Test]
    public async Task LocalEntityEventArgs_DoesNotReport()
    {
        // A bare EntityEventArgs is a local event (RaiseLocalEvent); a raw
        // EntityUid is correct there and must NOT be flagged.
        const string code = """
            using Robust.Shared.GameObjects;

            public sealed class FooEvent : EntityEventArgs
            {
                public EntityUid Target;
            }
            """;

        await Verify(code, "Content.Shared/RussStation/Foo/FooEvent.cs");
    }

    [Test]
    public async Task NullableEntityUid_ForkFile_Reports()
    {
        const string code = """
            using Robust.Shared.GameObjects;

            public sealed class FooState : BoundUserInterfaceState
            {
                public EntityUid? Target;
            }
            """;

        await Verify(code, "Content.Shared/RussStation/Foo/FooState.cs",
            new DiagnosticResult("HONK0025", DiagnosticSeverity.Warning)
                .WithSpan("Content.Shared/RussStation/Foo/FooState.cs", 5, 23, 5, 29)
                .WithArguments("Target"));
    }

    [Test]
    public async Task ListOfEntityUid_ForkFile_Reports()
    {
        const string code = """
            using System.Collections.Generic;
            using Robust.Shared.GameObjects;

            public sealed class FooState : BoundUserInterfaceState
            {
                public List<EntityUid> Targets;
            }
            """;

        await Verify(code, "Content.Shared/RussStation/Foo/FooState.cs",
            new DiagnosticResult("HONK0025", DiagnosticSeverity.Warning)
                .WithSpan("Content.Shared/RussStation/Foo/FooState.cs", 6, 28, 6, 35)
                .WithArguments("Targets"));
    }

    [Test]
    public async Task EntityUidArray_ForkFile_Reports()
    {
        const string code = """
            using Robust.Shared.GameObjects;

            public sealed class FooState : BoundUserInterfaceState
            {
                public EntityUid[] Targets;
            }
            """;

        await Verify(code, "Content.Shared/RussStation/Foo/FooState.cs",
            new DiagnosticResult("HONK0025", DiagnosticSeverity.Warning)
                .WithSpan("Content.Shared/RussStation/Foo/FooState.cs", 5, 24, 5, 31)
                .WithArguments("Targets"));
    }

    [Test]
    public async Task EntityUidProperty_ForkFile_Reports()
    {
        const string code = """
            using Robust.Shared.GameObjects;

            public sealed class FooState : BoundUserInterfaceState
            {
                public EntityUid Target { get; set; }
            }
            """;

        await Verify(code, "Content.Shared/RussStation/Foo/FooState.cs",
            new DiagnosticResult("HONK0025", DiagnosticSeverity.Warning)
                .WithSpan("Content.Shared/RussStation/Foo/FooState.cs", 5, 22, 5, 28)
                .WithArguments("Target"));
    }

    [Test]
    public async Task NetEntityField_DoesNotReport()
    {
        const string code = """
            using Robust.Shared.Serialization;

            [NetSerializable]
            public struct FooState
            {
                public NetEntity Target;
            }
            """;

        await Verify(code, "Content.Shared/RussStation/Foo/FooState.cs");
    }

    [Test]
    public async Task AutoNetworkedField_Excluded_DoesNotReport()
    {
        const string code = """
            using Robust.Shared.GameObjects;
            using Robust.Shared.Serialization;
            using Robust.Shared.Analyzers;

            [NetSerializable]
            public struct FooState
            {
                [AutoNetworkedField]
                public EntityUid Target;
            }
            """;

        await Verify(code, "Content.Shared/RussStation/Foo/FooState.cs");
    }

    [Test]
    public async Task PlainClass_EntityUidField_DoesNotReport()
    {
        const string code = """
            using Robust.Shared.GameObjects;

            public sealed class FooComponent
            {
                public EntityUid Target;
            }
            """;

        await Verify(code, "Content.Shared/RussStation/Foo/FooComponent.cs");
    }

    [Test]
    public async Task UpstreamFile_DoesNotReport()
    {
        const string code = """
            using Robust.Shared.GameObjects;

            public sealed class FooState : BoundUserInterfaceState
            {
                public EntityUid Target;
            }
            """;

        await Verify(code, "Content.Shared/Foo/FooState.cs");
    }

    [Test]
    public async Task HonkPartialFile_Reports()
    {
        const string code = """
            using Robust.Shared.GameObjects;

            public sealed class FooState : BoundUserInterfaceState
            {
                public EntityUid Target;
            }
            """;

        await Verify(code, "Content.Shared/Foo/FooState.Honk.cs",
            new DiagnosticResult("HONK0025", DiagnosticSeverity.Warning)
                .WithSpan("Content.Shared/Foo/FooState.Honk.cs", 5, 22, 5, 28)
                .WithArguments("Target"));
    }
}
