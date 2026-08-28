namespace BunnyTail.CommonCode;

using BunnyTail.CommonCode.Generator;

using Microsoft.CodeAnalysis;

public class DiagnosticTest
{
    // ------------------------------------------------------------
    // ToStringFormat
    // ------------------------------------------------------------

    [Fact]
    public void Btcc0103MaskCharAndMaskPatternEmitsDiagnostic()
    {
        // Arrange
        const string source =
            """
            using BunnyTail.CommonCode;

            namespace Test;

            [GenerateToString]
            public partial class Data
            {
                [ToStringFormat(MaskChar = '*', MaskPattern = "###")]
                public string? Name { get; set; }
            }
            """;

        // Act
        var diagnostics = GeneratorTestHelper.GetDiagnostics<ToStringGenerator>(source);

        // Assert
        Assert.Contains(diagnostics, static x => x.Id == "BTCC0103");
    }

    [Fact]
    public void Btcc0104NoEffectiveSettingEmitsDiagnostic()
    {
        // Arrange
        const string source =
            """
            using BunnyTail.CommonCode;

            namespace Test;

            [GenerateToString]
            public partial class Data
            {
                [ToStringFormat]
                public string? Name { get; set; }
            }
            """;

        // Act
        var diagnostics = GeneratorTestHelper.GetDiagnostics<ToStringGenerator>(source);

        // Assert
        Assert.Contains(diagnostics, static x => x.Id == "BTCC0104");
    }

    // ------------------------------------------------------------
    // DeepClone target
    // ------------------------------------------------------------

    [Fact]
    public void Btcc0302NotImplementIDeepCloneableEmitsDiagnostic()
    {
        // Arrange
        const string source =
            """
            using BunnyTail.CommonCode;

            namespace Test;

            [GenerateDeepClone]
            public partial class Data
            {
                public string Name { get; set; } = default!;
            }
            """;

        // Act
        var diagnostics = GeneratorTestHelper.GetDiagnostics<DeepCloneGenerator>(source);

        // Assert
        Assert.Contains(diagnostics, static x => x.Id == "BTCC0302");
    }

    // ------------------------------------------------------------
    // Type
    // ------------------------------------------------------------

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

    // ------------------------------------------------------------
    // Equality
    // ------------------------------------------------------------

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

    // ------------------------------------------------------------
    // DelegateTo
    // ------------------------------------------------------------

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

    // ------------------------------------------------------------
    // DeepClone
    // ------------------------------------------------------------

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

    // ------------------------------------------------------------
    // ToString
    // ------------------------------------------------------------

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

    // ------------------------------------------------------------
    // CompareTo
    // ------------------------------------------------------------

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

    // ------------------------------------------------------------
    // ToString
    // ------------------------------------------------------------

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

    [Fact]
    public void Abcd1234MaskPatternEmitsDiagnostic()
    {
        // Arrange
        var masked = new ToStringMaskPatternData
        {
            Password = "secret",
            Secret = "topsecret",
            Token = "abcd1234",
            Card = "4111111111111111"
        };
        var shortValue = new ToStringMaskPatternData
        {
            Password = "x",
            Secret = "y",
            Token = "ab",
            Card = "41111111"
        };
        var nullValue = new ToStringMaskPatternData();

        // Act
        var maskedText = masked.ToString();
        var shortText = shortValue.ToString();
        var nullText = nullValue.ToString();

        // Assert
        // A leading or trailing run of # keeps that many original characters visible
        Assert.Equal("ToStringMaskPatternData { Password = ***, Secret = [REDACTED], Token = ***34, Card = 4111****1111 }", maskedText);
        // A value not longer than the kept length is written as the mask text only
        Assert.Equal("ToStringMaskPatternData { Password = ***, Secret = [REDACTED], Token = ***, Card = **** }", shortText);
        // null is not masked and follows the null setting
        Assert.Equal("ToStringMaskPatternData { Password = null, Secret = null, Token = null, Card = null }", nullText);
    }
}
