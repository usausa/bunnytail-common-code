namespace BunnyTail.CommonCode;

using BunnyTail.CommonCode.Generator;

using Microsoft.CodeAnalysis;

public class DiagnosticTest
{
    //-----------------------------------------------------------------------
    // Type
    //-----------------------------------------------------------------------

    [Fact]
    public void Btcc0101NonPartialToStringEmitsDiagnostic()
    {
        var diagnostics = GeneratorTestHelper.GetDiagnostics<ToStringGenerator>(
            """
            using BunnyTail.CommonCode;

            namespace Test;

            [GenerateToString]
            public class Data
            {
                public int Id { get; set; }
            }
            """);

        Assert.Contains(diagnostics, static x => x.Id == "BTCC0101");
    }

    [Fact]
    public void Btcc0201NonPartialEqualityEmitsDiagnostic()
    {
        var diagnostics = GeneratorTestHelper.GetDiagnostics<EqualityGenerator>(
            """
            using BunnyTail.CommonCode;

            namespace Test;

            [GenerateEquality]
            public class Data
            {
                public int Id { get; set; }
            }
            """);

        Assert.Contains(diagnostics, static x => x.Id == "BTCC0201");
    }

    [Fact]
    public void Btcc0301NonPartialDeepCloneEmitsDiagnostic()
    {
        var diagnostics = GeneratorTestHelper.GetDiagnostics<DeepCloneGenerator>(
            """
            using BunnyTail.CommonCode;

            namespace Test;

            [GenerateDeepClone]
            public class Data
            {
                public int Id { get; set; }
            }
            """);

        Assert.Contains(diagnostics, static x => x.Id == "BTCC0301");
    }

    [Fact]
    public void Btcc0401NonPartialDelegateToEmitsDiagnostic()
    {
        var diagnostics = GeneratorTestHelper.GetDiagnostics<DelegateToGenerator>(
            """
            using BunnyTail.CommonCode;

            namespace Test;

            public interface IContract
            {
                void Run();
            }

            [GenerateDelegateTo]
            public class Data
            {
                [DelegateTo]
                private readonly IContract inner = default!;
            }
            """);

        Assert.Contains(diagnostics, static x => x.Id == "BTCC0401");
    }

    [Fact]
    public void Btcc0501NonPartialCompareToEmitsDiagnostic()
    {
        var diagnostics = GeneratorTestHelper.GetDiagnostics<CompareToGenerator>(
            """
            using BunnyTail.CommonCode;

            namespace Test;

            [GenerateCompareTo]
            public class Data
            {
                [CompareKey]
                public int Id { get; set; }
            }
            """);

        Assert.Contains(diagnostics, static x => x.Id == "BTCC0501");
    }

    //-----------------------------------------------------------------------
    // Equality
    //-----------------------------------------------------------------------

    [Fact]
    public void Btcc0202NoPropertyEmitsDiagnostic()
    {
        var diagnostics = GeneratorTestHelper.GetDiagnostics<EqualityGenerator>(
            """
            using BunnyTail.CommonCode;

            namespace Test;

            [GenerateEquality]
            public partial class Data
            {
            }
            """);

        Assert.Contains(diagnostics, static x => x.Id == "BTCC0202");
    }

    //-----------------------------------------------------------------------
    // DelegateTo
    //-----------------------------------------------------------------------

    [Fact]
    public void Btcc0402NoDelegateFieldEmitsDiagnostic()
    {
        var diagnostics = GeneratorTestHelper.GetDiagnostics<DelegateToGenerator>(
            """
            using BunnyTail.CommonCode;

            namespace Test;

            [GenerateDelegateTo]
            public partial class Data
            {
                public int Id { get; set; }
            }
            """);

        Assert.Contains(diagnostics, static x => x.Id == "BTCC0402");
    }

    [Fact]
    public void Btcc0403InvalidInterfaceTypeEmitsDiagnostic()
    {
        var diagnostics = GeneratorTestHelper.GetDiagnostics<DelegateToGenerator>(
            """
            using BunnyTail.CommonCode;

            namespace Test;

            public interface IContract
            {
                void Run();
            }

            public sealed class NotAnImplementation
            {
            }

            [GenerateDelegateTo]
            public partial class Data
            {
                [DelegateTo(InterfaceType = typeof(IContract))]
                private readonly NotAnImplementation inner = default!;
            }
            """);

        Assert.Contains(diagnostics, static x => x.Id == "BTCC0403");
    }

    //-----------------------------------------------------------------------
    // DeepClone
    //-----------------------------------------------------------------------

    [Fact]
    public void Btcc0303PropertyMissingDeepCloneEmitsDiagnostic()
    {
        var diagnostics = GeneratorTestHelper.GetDiagnostics<DeepCloneGenerator>(
            """
            using BunnyTail.CommonCode;

            namespace Test;

            public sealed class Inner
            {
            }

            [GenerateDeepClone]
            public partial class Data : IDeepCloneable<Data>
            {
                public Inner Value { get; set; } = default!;
            }
            """);

        Assert.Contains(diagnostics, static x => x.Id == "BTCC0303");
    }

    //-----------------------------------------------------------------------
    // ToString
    //-----------------------------------------------------------------------

    [Fact]
    public void Btcc0102FormatOnIgnoredEmitsDiagnostic()
    {
        var diagnostics = GeneratorTestHelper.GetDiagnostics<ToStringGenerator>(
            """
            using BunnyTail.CommonCode;

            namespace Test;

            [GenerateToString]
            public partial class Data
            {
                [IgnoreToString]
                [ToStringFormat("X")]
                public int Id { get; set; }
            }
            """);

        Assert.Contains(diagnostics, static x => x.Id == "BTCC0102");
    }

    //-----------------------------------------------------------------------
    // CompareTo
    //-----------------------------------------------------------------------

    [Fact]
    public void Btcc0502NoCompareKeyEmitsDiagnostic()
    {
        var diagnostics = GeneratorTestHelper.GetDiagnostics<CompareToGenerator>(
            """
            using BunnyTail.CommonCode;

            namespace Test;

            [GenerateCompareTo]
            public partial class Data
            {
                public int Id { get; set; }
            }
            """);

        Assert.Contains(diagnostics, static x => x.Id == "BTCC0502");
    }

    //-----------------------------------------------------------------------
    // ToString
    //-----------------------------------------------------------------------

    [Fact]
    public void ValidToStringEmitsNoDiagnostic()
    {
        var diagnostics = GeneratorTestHelper.GetDiagnostics<ToStringGenerator>(
            """
            using BunnyTail.CommonCode;

            namespace Test;

            [GenerateToString]
            public partial class Data
            {
                public int Id { get; set; }
            }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void ValidEqualityEmitsNoDiagnostic()
    {
        var diagnostics = GeneratorTestHelper.GetDiagnostics<EqualityGenerator>(
            """
            using BunnyTail.CommonCode;

            namespace Test;

            [GenerateEquality]
            public partial class Data
            {
                public int Id { get; set; }
            }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void ValidCompareToGeneratesSource()
    {
        var generated = GeneratorTestHelper.GetGeneratedSource<CompareToGenerator>(
            """
            using BunnyTail.CommonCode;

            namespace Test;

            [GenerateCompareTo]
            public partial class Data
            {
                [CompareKey]
                public int Id { get; set; }
            }
            """);

        Assert.Contains("CompareTo", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidEqualityProducesNoCompilationError()
    {
        var diagnostics = GeneratorTestHelper.GetDiagnosticsAll<EqualityGenerator>(
            """
            using BunnyTail.CommonCode;

            namespace Test;

            [GenerateEquality]
            public partial class Data
            {
                public int Id { get; set; }
            }
            """);

        Assert.DoesNotContain(diagnostics, static x => x.Severity == DiagnosticSeverity.Error);
    }
}
