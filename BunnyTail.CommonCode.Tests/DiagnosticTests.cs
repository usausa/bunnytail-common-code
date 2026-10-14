namespace BunnyTail.CommonCode;

using System.Globalization;
using System.Reflection;

using BunnyTail.CommonCode.Generator;

using Microsoft.CodeAnalysis;

public class DiagnosticTests
{
    // ------------------------------------------------------------
    // Common (00xx)
    // ------------------------------------------------------------

    [Fact]
    public void Btcc0001TypeNamesDifferingOnlyInCaseEmitDiagnostic()
    {
        // Arrange
        const string source =
            """
            using BunnyTail.CommonCode;

            namespace Test;

            [GenerateToString]
            public partial class DataItem
            {
                public int Id { get; set; }
            }

            [GenerateToString]
            public partial class Dataitem
            {
                public int Id { get; set; }
            }
            """;

        // Act
        var result = GeneratorTestHelper.Run<ToStringGenerator>(source);

        // Assert
        var diagnostic = Assert.Single(result.Problems);
        Assert.Equal("BTCC0001", diagnostic.Id);
        Assert.Contains("[GenerateToString]", diagnostic.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        Assert.Contains("type=[Test.Dataitem], other=[Test.DataItem]", diagnostic.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        Assert.Single(result.GeneratedSources);
    }

    [Fact]
    public void AttributeOnSeveralPartialDeclarationsGeneratesOnce()
    {
        // Arrange
        const string source =
            """
            using BunnyTail.CommonCode;

            namespace Test;

            [GenerateEquality]
            public partial class Data
            {
                public int Id { get; set; }
            }

            [GenerateEquality]
            public partial class Data
            {
                public string? Name { get; set; }
            }
            """;

        // Act
        var result = GeneratorTestHelper.Run<EqualityGenerator>(source);

        // Assert
        Assert.DoesNotContain(result.Problems, static x => x.Id == "CS8785");
        Assert.Single(result.GeneratedSources);
    }

    // ------------------------------------------------------------
    // ToString (01xx)
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
    public void Btcc0101TypeInNonPartialTypeEmitsDiagnostic()
    {
        var problems = GeneratorTestHelper.GetProblemIds(
            """
            using BunnyTail.CommonCode;

            namespace Test;

            public class Outer
            {
                [GenerateToString]
                public partial class Data
                {
                    public int Id { get; set; }
                }
            }
            """);

        Assert.Equal(["BTCC0101"], problems);
    }

    [Fact]
    public void Btcc0101FileLocalTypeEmitsDiagnostic()
    {
        var problems = GeneratorTestHelper.GetProblemIds(
            """
            using BunnyTail.CommonCode;

            namespace Test;

            [GenerateToString]
            file partial class Data
            {
                public int Id { get; set; }
            }
            """);

        Assert.Equal(["BTCC0101"], problems);
    }

    [Fact]
    public void Btcc0102SealedBaseToStringEmitsDiagnostic()
    {
        var problems = GeneratorTestHelper.GetProblemIds(
            """
            using BunnyTail.CommonCode;

            namespace Test;

            public class Base
            {
                public sealed override string ToString() => "base";
            }

            public class Middle : Base
            {
            }

            [GenerateToString]
            public partial class Data : Middle
            {
                public int Id { get; set; }
            }
            """);

        Assert.Equal(["BTCC0102"], problems);
    }

    [Fact]
    public void Btcc0102SealedBaseRecordToStringEmitsDiagnostic()
    {
        var problems = GeneratorTestHelper.GetProblemIds(
            """
            using BunnyTail.CommonCode;

            namespace Test;

            public record BaseRecord
            {
                public sealed override string ToString() => "base";
            }

            [GenerateToString]
            public partial record DataRecord : BaseRecord
            {
                public int Id { get; init; }
            }
            """);

        Assert.Equal(["BTCC0102"], problems);
    }

    [Fact]
    public void Btcc0103RefStructMemberEmitsDiagnostic()
    {
        // Arrange
        const string source =
            """
            using System;

            using BunnyTail.CommonCode;

            namespace Test;

            [GenerateToString]
            public ref partial struct Data
            {
                public Span<int> Values { get; set; }

                public ReadOnlySpan<char> Name { get; set; }

                public int Id { get; set; }
            }
            """;

        // Act
        var result = GeneratorTestHelper.Run<ToStringGenerator>(source);

        // Assert
        var diagnostic = Assert.Single(result.Problems);
        Assert.Equal("BTCC0103", diagnostic.Id);
        Assert.Contains("member=[Values]", diagnostic.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        Assert.DoesNotContain("this.Values", result.AllGeneratedText, StringComparison.Ordinal);
    }

    [Fact]
    public void RefStructFieldIsReportedOnlyWhenFieldsAreWritten()
    {
        // Arrange
        const string source =
            """
            using System;

            using BunnyTail.CommonCode;

            namespace Test;

            [GenerateToString]
            public ref partial struct Data
            {
                public Span<int> Values;

                public int Id { get; set; }
            }
            """;

        // Act
        var propertyOnly = GeneratorTestHelper.Run<ToStringGenerator>(source);
        var withField = GeneratorTestHelper.RunWithOption<ToStringGenerator>("CommonCodeGeneratorToStringMembers", "PropertyAndField", source);

        // Assert
        Assert.Empty(propertyOnly.Problems);
        Assert.Equal(["BTCC0103"], withField.Problems.Select(static x => x.Id));
    }

    [Theory]
    [InlineData("CommonCodeGeneratorToStringTypeName", "Short")]
    [InlineData("CommonCodeGeneratorToStringTypeName", "5")]
    [InlineData("CommonCodeGeneratorToStringCollectionLimit", "many")]
    [InlineData("CommonCodeGeneratorToStringSkipLocalsInit", "yes")]
    public void Btcc0104InvalidOptionValueEmitsDiagnostic(string name, string value)
    {
        // Arrange
        const string source =
            """
            using BunnyTail.CommonCode;

            namespace Test;

            [GenerateToString]
            public partial class Data
            {
                public int Id { get; set; }
            }
            """;

        // Act
        var result = GeneratorTestHelper.RunWithOption<ToStringGenerator>(name, value, source);

        // Assert
        var diagnostic = Assert.Single(result.Problems);
        Assert.Equal("BTCC0104", diagnostic.Id);
        Assert.Contains($"property=[{name}], value=[{value}]", diagnostic.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        Assert.Contains("\"Data { Id = \"", result.AllGeneratedText, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyOptionValueIsNotReported()
    {
        // Arrange
        const string source =
            """
            using BunnyTail.CommonCode;

            namespace Test;

            [GenerateToString]
            public partial class Data
            {
                public int Id { get; set; }
            }
            """;

        // Act
        var result = GeneratorTestHelper.RunWithOption<ToStringGenerator>("CommonCodeGeneratorToStringTypeName", string.Empty, source);

        // Assert
        Assert.Empty(result.Problems);
    }

    [Fact]
    public void EscapedStringOptionIsUnescaped()
    {
        // Arrange
        const string source =
            """
            using BunnyTail.CommonCode;

            namespace Test;

            [GenerateToString]
            public partial class Data
            {
                public int Id { get; set; }

                public string Name { get; set; } = string.Empty;
            }
            """;

        // Act
        var result = GeneratorTestHelper.RunWithOption<ToStringGenerator>("_CommonCodeGeneratorToStringSeparator", "\"%23%3B \"", source);

        // Assert
        Assert.Empty(result.Problems);
        Assert.Contains("#; Name = ", result.AllGeneratedText, StringComparison.Ordinal);
    }

    [Fact]
    public void Btcc0105FormatOnIgnoredEmitsDiagnostic()
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

        Assert.Contains(diagnostics, static x => x.Id == "BTCC0105");
    }

    [Fact]
    public void Btcc0106MaskCharAndMaskPatternEmitsDiagnostic()
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
        Assert.Contains(diagnostics, static x => x.Id == "BTCC0106");
    }

    [Fact]
    public void Btcc0107NoEffectiveSettingEmitsDiagnostic()
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
        Assert.Contains(diagnostics, static x => x.Id == "BTCC0107");
    }

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

    // ------------------------------------------------------------
    // Equality (02xx)
    // ------------------------------------------------------------

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
    public void Btcc0202RefStructPropertyEmitsDiagnostic()
    {
        var problems = GeneratorTestHelper.GetProblemIds(
            """
            using System;

            using BunnyTail.CommonCode;

            namespace Test;

            [GenerateEquality]
            public ref partial struct Data
            {
                public int Id { get; set; }

                public ReadOnlySpan<char> Name { get; set; }
            }
            """);

        Assert.Equal(["BTCC0202"], problems);
    }

    [Fact]
    public void Btcc0203NoPropertyEmitsDiagnostic()
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

        Assert.Contains(diagnostics, static x => x.Id == "BTCC0203");
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

    // ------------------------------------------------------------
    // DeepClone (03xx)
    // ------------------------------------------------------------

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
    public void Btcc0302RefStructEmitsDiagnostic()
    {
        var problems = GeneratorTestHelper.GetProblemIds(
            """
            using BunnyTail.CommonCode;

            namespace Test;

            [GenerateDeepClone]
            public ref partial struct Data
            {
                public int Id { get; set; }
            }
            """);

        Assert.Equal(["BTCC0302"], problems);
    }

    [Fact]
    public void Btcc0303NotImplementIDeepCloneableEmitsDiagnostic()
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
        Assert.Contains(diagnostics, static x => x.Id == "BTCC0303");
    }

    [Fact]
    public void Btcc0304PropertyMissingDeepCloneEmitsDiagnostic()
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

        Assert.Contains(diagnostics, static x => x.Id == "BTCC0304");
    }

    // ------------------------------------------------------------
    // DelegateTo (04xx)
    // ------------------------------------------------------------

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
    public void Btcc0402InvalidInterfaceTypeEmitsDiagnostic()
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

        var diagnostic = Assert.Single(diagnostics, static x => x.Id == "BTCC0402");
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
    }

    [Fact]
    public void Btcc0403NoDelegateFieldEmitsDiagnostic()
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

        Assert.Contains(diagnostics, static x => x.Id == "BTCC0403");
    }

    // ------------------------------------------------------------
    // CompareTo (05xx)
    // ------------------------------------------------------------

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

    [Fact]
    public void Btcc0502KeyNotComparableEmitsDiagnostic()
    {
        // Arrange
        const string source =
            """
            using System;
            using System.Collections.Generic;

            using BunnyTail.CommonCode;

            namespace Test;

            public struct Plain
            {
                public int Value { get; set; }
            }

            public sealed class SealedPlain
            {
            }

            public class OpenPlain
            {
            }

            public class Animal : IComparable<Animal>
            {
                public int CompareTo(Animal? other) => 0;
            }

            public sealed class Dog : Animal
            {
            }

            [GenerateCompareTo]
            public partial class Data
            {
                [CompareKey(Order = 1)]
                public Plain Struct { get; set; }

                [CompareKey(Order = 2)]
                public SealedPlain? Sealed { get; set; }

                [CompareKey(Order = 3)]
                public int[]? Array { get; set; }

                [CompareKey(Order = 4)]
                public KeyValuePair<int, int> Pair { get; set; }

                [CompareKey(Order = 5)]
                public OpenPlain? Open { get; set; }

                [CompareKey(Order = 6)]
                public int? Nullable { get; set; }

                [CompareKey(Order = 7)]
                public string? Text { get; set; }

                [CompareKey(Order = 8)]
                public DayOfWeek Day { get; set; }

                [CompareKey(Order = 9)]
                public Dog? Pet { get; set; }

                [CompareKey(Order = 10)]
                public (int, string) Tuple { get; set; }
            }
            """;

        // Act
        var result = GeneratorTestHelper.Run<CompareToGenerator>(source);

        // Assert
        Assert.Equal(4, result.Problems.Count);
        Assert.All(result.Problems, static x => Assert.Equal("BTCC0502", x.Id));
        foreach (var name in new[] { "Struct", "Sealed", "Array", "Pair" })
        {
            Assert.Contains(result.Problems, x => x.GetMessage(CultureInfo.InvariantCulture).Contains($"member=[{name}]", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Btcc0503NoCompareKeyEmitsDiagnostic()
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

        Assert.Contains(diagnostics, static x => x.Id == "BTCC0503");
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

    // ------------------------------------------------------------
    // Reporting
    // ------------------------------------------------------------

    [Fact]
    public void DiagnosticIsReportedInSource()
    {
        // Arrange
        const string source =
            """
            using BunnyTail.CommonCode;

            namespace Test;

            [GenerateToString]
            public class Data
            {
                public int Id { get; set; }
            }
            """;

        // Act
        var diagnostics = GeneratorTestHelper.GetDiagnostics<ToStringGenerator>(source);

        // Assert
        var diagnostic = Assert.Single(diagnostics, static x => x.Id == "BTCC0101");
        Assert.True(diagnostic.Location.IsInSource);
    }

    [Fact]
    public void ErrorsCannotBeSuppressed()
    {
        // Arrange
        var descriptors = typeof(ToStringGenerator).Assembly.GetType("BunnyTail.CommonCode.Generator.Diagnostics", throwOnError: true)!
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(static x => x.PropertyType == typeof(DiagnosticDescriptor))
            .Select(static x => (DiagnosticDescriptor)x.GetValue(null)!)
            .ToList();

        // Assert
        Assert.All(
            descriptors.Where(static x => x.DefaultSeverity == DiagnosticSeverity.Error),
            static x => Assert.Equal([WellKnownDiagnosticTags.NotConfigurable, WellKnownDiagnosticTags.Compiler], x.CustomTags));
    }
}
