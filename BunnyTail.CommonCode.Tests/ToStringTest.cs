// ReSharper disable StringLiteralTypo
namespace BunnyTail.CommonCode;

#pragma warning disable CA1819
// ReSharper disable once PartialTypeWithSinglePart
[GenerateToString]
public partial class ToStringData
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public int[]? IntValues { get; set; }

    public string?[]? StringValues { get; set; }

    [IgnoreToString]
    public int Ignore { get; set; }
}
#pragma warning restore CA1819

// ReSharper disable once PartialTypeWithSinglePart
[GenerateToString]
public partial class ToStringEmptyData;

// ReSharper disable once PartialTypeWithSinglePart
[GenerateToString]
public partial class ToStringGenericData<T>
{
    public T Value { get; set; } = default!;
}

// ReSharper disable once PartialTypeWithSinglePart
[GenerateToString]
public partial class ToStringGenericPairData<TKey, TValue>
{
    public TKey Key { get; set; } = default!;

    public TValue Value { get; set; } = default!;
}

// ReSharper disable once PartialTypeWithSinglePart
#pragma warning disable CA1034
[GenerateToString]
public partial class ToStringEmptyGenericData<T>;

public static partial class ToStringOuterData
{
    [GenerateToString]
    public partial class InnerData
    {
        public int Id { get; set; }

        public string Name { get; set; } = default!;
    }
}
#pragma warning restore CA1034

public class ToStringShadowBase
{
    public int Value { get; init; }
}

// Even when a derived type hides a base property with new, only the most-derived declaration is output (no duplicate output)
[GenerateToString]
public partial class ToStringShadowDerived : ToStringShadowBase
{
    public new string Value { get; init; } = default!;
}

// The indexer is excluded from output
[GenerateToString]
public partial class ToStringIndexedData
{
    private readonly Dictionary<string, string> map = [];

    public string Name { get; init; } = default!;

    public string this[string key]
    {
        get => map[key];
        set => map[key] = value;
    }
}

public class ToStringHiddenBase
{
    public string Token { get; init; } = default!;
}

// The derived type hides the public member with new marked IgnoreToString. Since this.Token binds to the derived one and cannot reach the base public one, it is excluded from output
[GenerateToString]
public partial class ToStringHiddenDerived : ToStringHiddenBase
{
    [IgnoreToString]
    public new int Token { get; init; }

    public string Label { get; init; } = default!;
}

public class ToStringInheritanceBase
{
    public int BaseFirst { get; init; }

    public int BaseSecond { get; init; }
}

// Base type members are output first
[GenerateToString]
public partial class ToStringInheritanceDerived : ToStringInheritanceBase
{
    public int DerivedFirst { get; init; }

    public int DerivedSecond { get; init; }
}

// Static members are excluded from output
[GenerateToString]
public partial class ToStringStaticData
{
    public int Id { get; set; }

    public static int Instances { get; set; }
}

#pragma warning disable CA1051
#pragma warning disable SA1401
// Fields are excluded unless CommonCodeGeneratorToStringMembers is PropertyAndField
[GenerateToString]
public partial class ToStringFieldData
{
    public int Id { get; set; }

    public int Extra;
}
#pragma warning restore SA1401
#pragma warning restore CA1051

#pragma warning disable CA1815
[GenerateToString]
public partial struct ToStringStructData
{
    public int X { get; set; }

    public int Y { get; set; }
}
#pragma warning restore CA1815

[GenerateToString]
public partial class ToStringMaskPatternData
{
    [ToStringFormat(MaskPattern = "***")]
    public string Password { get; set; } = default!;

    [ToStringFormat(MaskPattern = "[REDACTED]")]
    public string Secret { get; set; } = default!;

    [ToStringFormat(MaskPattern = "***##")]
    public string Token { get; set; } = default!;

    [ToStringFormat(MaskPattern = "####****####")]
    public string Card { get; set; } = default!;
}

[GenerateToString]
public partial class ToStringMaskCharData
{
    [ToStringFormat(MaskChar = '*')]
    public string Password { get; set; } = default!;

    [ToStringFormat(MaskChar = '.')]
    public string Secret { get; set; } = default!;
}

#pragma warning disable CA1819
[GenerateToString]
public partial class ToStringMaskedCollectionData
{
    [ToStringFormat(MaskPattern = "***")]
    public string[]? Secrets { get; set; }

    public int[]? Values { get; set; }
}
#pragma warning restore CA1819

[GenerateToString]
public partial class ToStringMaskedValueTypeData
{
    [ToStringFormat("000000", MaskPattern = "##****")]
    public int Number { get; set; }
}

// Format, masking and MaxLength are applied in this order
[GenerateToString]
public partial class ToStringMaskWithMaxLengthData
{
    [ToStringFormat(MaskChar = '*', MaxLength = 4)]
    public string Password { get; set; } = default!;

    [ToStringFormat(MaskPattern = "[REDACTED]", MaxLength = 4)]
    public string Secret { get; set; } = default!;

    [ToStringFormat(MaskPattern = "####****####", MaxLength = 8)]
    public string Card { get; set; } = default!;

    [ToStringFormat(MaskPattern = "####****####", MaxLength = 2)]
    public string Account { get; set; } = default!;

    [ToStringFormat("000000", MaskChar = '*', MaxLength = 4)]
    public int Number { get; set; }
}

[GenerateToString]
public partial class ToStringFormatData
{
    [ToStringFormat("000")]
    public int Code { get; set; }

    [ToStringFormat("X4")]
    public int Hex { get; set; }
}

[GenerateToString]
public partial class ToStringMaxLengthData
{
    [ToStringFormat(MaxLength = 3)]
    public string Description { get; set; } = default!;

    public string Name { get; set; } = default!;
}

[GenerateToString]
public partial class ToStringFormatMaxLengthData
{
    [ToStringFormat("000000", MaxLength = 3)]
    public int Number { get; set; }
}

public class ToStringTest
{
    [Fact]
    public void TestBasic()
    {
        // Arrange
        var withValues = new ToStringData { Id = 123, Name = "xyz", IntValues = [1, 2], StringValues = ["a", null] };
        var withNulls = new ToStringData { Id = 123 };
        var withEmpty = new ToStringData { Id = 123, IntValues = [], StringValues = [] };

        // Act
        var withValuesText = withValues.ToString();
        var withNullsText = withNulls.ToString();
        var withEmptyText = withEmpty.ToString();

        // Assert
        Assert.Equal("ToStringData { Id = 123, Name = xyz, IntValues = [1, 2], StringValues = [a, null] }", withValuesText);
        Assert.Equal("ToStringData { Id = 123, Name = null, IntValues = null, StringValues = null }", withNullsText);
        Assert.Equal("ToStringData { Id = 123, Name = null, IntValues = [], StringValues = [] }", withEmptyText);
    }

    [Fact]
    public void TestEmpty()
    {
        // Arrange
        var data = new ToStringEmptyData();

        // Act
        var text = data.ToString();

        // Assert
        Assert.Equal("ToStringEmptyData { }", text); // The inner space is collapsed into one
    }

    [Fact]
    public void TestGenericTypeArgument()
    {
        // Arrange
        var intData = new ToStringGenericData<int> { Value = 123 };
        var stringData = new ToStringGenericData<string> { Value = "xyz" };
        var nullData = new ToStringGenericData<string?> { Value = null };
        var nullableData = new ToStringGenericData<int?> { Value = 1 };
        var arrayData = new ToStringGenericData<int[]> { Value = [1] };
        var nestedData = new ToStringGenericData<List<int>> { Value = [1] };
        var pairData = new ToStringGenericPairData<int, string> { Key = 1, Value = "v" };
        var emptyData = new ToStringEmptyGenericData<int>();

        // Act & Assert
        // The runtime type arguments are output using the C# keyword
        Assert.Equal("ToStringGenericData<int> { Value = 123 }", intData.ToString());
        Assert.Equal("ToStringGenericData<string> { Value = xyz }", stringData.ToString());
        Assert.Equal("ToStringGenericData<string> { Value = null }", nullData.ToString());
        Assert.Equal("ToStringGenericData<int?> { Value = 1 }", nullableData.ToString());
        Assert.Equal("ToStringGenericData<int[]> { Value = System.Int32[] }", arrayData.ToString());
        Assert.Equal("ToStringGenericData<List<int>> { Value = System.Collections.Generic.List`1[System.Int32] }", nestedData.ToString());
        Assert.Equal("ToStringGenericPairData<int, string> { Key = 1, Value = v }", pairData.ToString());
        Assert.Equal("ToStringEmptyGenericData<int> { }", emptyData.ToString());
    }

    [Fact]
    public void TestInnerClass()
    {
        // Arrange
        var data = new ToStringOuterData.InnerData { Id = 456, Name = "inner" };

        // Act
        var text = data.ToString();

        // Assert
        Assert.Equal("InnerData { Id = 456, Name = inner }", text); // Only the innermost name is output
    }

    [Fact]
    public void TestShadowedProperty()
    {
        // Arrange
        var data = new ToStringShadowDerived { Value = "x" };

        // Act
        var text = data.ToString();

        // Assert
        // The hidden base int Value is not output, only the most-derived string Value (no duplicate output)
        Assert.Equal("ToStringShadowDerived { Value = x }", text);
    }

    [Fact]
    public void TestIndexerExcluded()
    {
        // Arrange
        var data = new ToStringIndexedData
        {
            Name = "x",
            ["k"] = "v"
        };

        // Act
        var text = data.ToString();

        // Assert
        Assert.Equal("ToStringIndexedData { Name = x }", text); // The indexer is excluded from output
    }

    [Fact]
    public void TestHiddenPropertyExcluded()
    {
        // Arrange
        var data = new ToStringHiddenDerived { Token = 1, Label = "L" };

        // Act
        var text = data.ToString();

        // Assert
        // The hidden + IgnoreToString Token is not output, only Label
        Assert.Equal("ToStringHiddenDerived { Label = L }", text);
    }

    [Fact]
    public void TestInheritanceOrder()
    {
        // Arrange
        var data = new ToStringInheritanceDerived { BaseFirst = 1, BaseSecond = 2, DerivedFirst = 3, DerivedSecond = 4 };

        // Act
        var text = data.ToString();

        // Assert
        // Base type members come first
        Assert.Equal("ToStringInheritanceDerived { BaseFirst = 1, BaseSecond = 2, DerivedFirst = 3, DerivedSecond = 4 }", text);
    }

    [Fact]
    public void TestStaticExcluded()
    {
        // Arrange
        ToStringStaticData.Instances = 99;
        var data = new ToStringStaticData { Id = 1 };

        // Act
        var text = data.ToString();

        // Assert
        Assert.Equal("ToStringStaticData { Id = 1 }", text); // Static members are excluded from output
    }

    [Fact]
    public void TestFieldExcluded()
    {
        // Arrange
        var data = new ToStringFieldData { Id = 1, Extra = 2 };

        // Act
        var text = data.ToString();

        // Assert
        Assert.Equal("ToStringFieldData { Id = 1 }", text); // Fields are excluded by default
    }

    [Fact]
    public void TestStruct()
    {
        // Arrange
        var data = new ToStringStructData { X = 1, Y = 2 };

        // Act
        var text = data.ToString();

        // Assert
        Assert.Equal("ToStringStructData { X = 1, Y = 2 }", text);
    }

    [Fact]
    public void TestMaskChar()
    {
        // Arrange
        var masked = new ToStringMaskCharData { Password = "secret", Secret = "abc" };
        var nullValue = new ToStringMaskCharData();

        // Act
        var maskedText = masked.ToString();
        var nullText = nullValue.ToString();

        // Assert
        // MaskChar repeats the character over the whole value
        Assert.Equal("ToStringMaskCharData { Password = ******, Secret = ... }", maskedText);
        Assert.Equal("ToStringMaskCharData { Password = null, Secret = null }", nullText);
    }

    [Fact]
    public void TestMaskWithMaxLength()
    {
        // Arrange
        var longValue = new ToStringMaskWithMaxLengthData
        {
            Password = "secretvalue",
            Secret = "topsecret",
            Card = "4111111111111111",
            Account = "4111111111111111",
            Number = 7
        };
        var shortValue = new ToStringMaskWithMaxLengthData
        {
            Password = "ab",
            Secret = "y",
            Card = "41111111",
            Account = "41111111"
        };

        // Act
        var longText = longValue.ToString();
        var shortText = shortValue.ToString();

        // Assert
        // MaxLength truncates the masked result: ********** -> ****, [REDACTED] -> [RED,
        // 4111****1111 -> 4111****, 4111****1111 -> 41, 000007 -> ****** -> ****
        Assert.Equal("ToStringMaskWithMaxLengthData { Password = ****, Secret = [RED, Card = 4111****, Account = 41, Number = **** }", longText);
        // A value not longer than the kept length is masked over its whole length, then truncated
        Assert.Equal("ToStringMaskWithMaxLengthData { Password = **, Secret = [RED, Card = ****, Account = **, Number = **** }", shortText);
    }

    [Fact]
    public void TestMaskWithFormat()
    {
        // Arrange
        var data = new ToStringMaskedValueTypeData { Number = 7 };

        // Act
        var text = data.ToString();

        // Assert
        // The format is applied before masking: 7 -> "000007" -> "00****"
        Assert.Equal("ToStringMaskedValueTypeData { Number = 00**** }", text);
    }

    [Fact]
    public void TestMaskedCollection()
    {
        // Arrange
        var data = new ToStringMaskedCollectionData { Secrets = ["a", "b"], Values = [1, 2] };

        // Act
        var text = data.ToString();

        // Assert
        // Mask wins over collection expansion, so the elements are never written
        Assert.Equal("ToStringMaskedCollectionData { Secrets = ***, Values = [1, 2] }", text);
    }

    [Fact]
    public void TestFormat()
    {
        // Arrange
        var data = new ToStringFormatData { Code = 7, Hex = 255 };

        // Act
        var text = data.ToString();

        // Assert
        Assert.Equal("ToStringFormatData { Code = 007, Hex = 00FF }", text); // The ToStringFormat format is applied
    }

    [Fact]
    public void TestMaxLength()
    {
        // Arrange
        var longValue = new ToStringMaxLengthData { Description = "abcdef", Name = "x" };
        var withinLimit = new ToStringMaxLengthData { Description = "ab", Name = "y" };
        var nullValue = new ToStringMaxLengthData { Name = "z" };

        // Act
        var longText = longValue.ToString();
        var withinText = withinLimit.ToString();
        var nullText = nullValue.ToString();

        // Assert
        Assert.Equal("ToStringMaxLengthData { Description = abc, Name = x }", longText);  // Truncated to the maximum length
        Assert.Equal("ToStringMaxLengthData { Description = ab, Name = y }", withinText); // Within the limit, kept as is
        Assert.Equal("ToStringMaxLengthData { Description = null, Name = z }", nullText); // null is excluded from truncation
    }

    [Fact]
    public void TestFormatWithMaxLength()
    {
        // Arrange
        var data = new ToStringFormatMaxLengthData { Number = 7 };

        // Act
        var text = data.ToString();

        // Assert
        // Truncated after applying the format: 7 -> "000007" -> "000"
        Assert.Equal("ToStringFormatMaxLengthData { Number = 000 }", text);
    }
}
