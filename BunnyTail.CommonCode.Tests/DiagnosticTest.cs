namespace BunnyTail.CommonCode;

using BunnyTail.CommonCode.Generator;

using Microsoft.CodeAnalysis;

// Diagnostic coverage for the five generators in this project.
// The other tests exercise generated code at run time and therefore only reach inputs the
// generators accept; these cover the refusals.
public class DiagnosticTest
{
    //-----------------------------------------------------------------------
    // BTCC0101 / 0201 / 0301 / 0401 / 0501 : the type must be partial
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
    // BTCC0202 : equality target has no property
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
    // BTCC0402 : no field carries [DelegateTo]
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

    //-----------------------------------------------------------------------
    // BTCC0502 : no property carries [CompareKey]
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
    // Valid input must stay clean
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
