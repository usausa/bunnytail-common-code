namespace BunnyTail.CommonCode;

// ------------------------------------------------------------
// Default style
// ------------------------------------------------------------

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
[GenerateToString]
public partial class ToStringEmptyGenericData<T>;

// ReSharper disable once PartialTypeWithSinglePart
[GenerateToString(TypeArgument = ToStringTypeArgument.None)]
public partial class ToStringNoTypeArgumentData<T>
{
    public T Value { get; set; } = default!;
}

public static partial class ToStringOuterData
{
    [GenerateToString]
    public partial class InnerData
    {
        public int Id { get; set; }

        public string Name { get; set; } = default!;
    }

    [GenerateToString(TypeName = ToStringTypeName.Full)]
    public partial class FullNameData
    {
        public int Id { get; set; }
    }
}

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

// Base type members are output first, same as record
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
[GenerateToString]
public partial class ToStringPropertyOnlyData
{
    public int Id { get; set; }

    public int Extra;
}

[GenerateToString(Members = ToStringMemberKind.PropertyAndField)]
public partial class ToStringFieldData
{
    public int Id { get; set; }

    public int Extra;

    [IgnoreToString]
    public int Ignore;
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

// ------------------------------------------------------------
// Record style
// ------------------------------------------------------------

public record ToStringRecordEquivalent
{
    public int Id { get; init; }

    public string? Name { get; init; }

    public int? Number { get; init; }
}

// ReSharper disable once PartialTypeWithSinglePart
[GenerateToString(Style = ToStringStyle.Record)]
public partial class ToStringRecordCompatData
{
    public int Id { get; init; }

    public string? Name { get; init; }

    public int? Number { get; init; }
}

#pragma warning disable CA1819
[GenerateToString(Style = ToStringStyle.Record)]
public partial class ToStringRecordStyleData
{
    public string? Name { get; set; }

    public int[]? Values { get; set; }
}
#pragma warning restore CA1819

// ------------------------------------------------------------
// Null
// ------------------------------------------------------------

[GenerateToString(NullLiteral = "<null>")]
public partial class ToStringNullLiteralData
{
    public string? Name { get; set; }
}

[GenerateToString(Null = ToStringNullMode.Empty)]
public partial class ToStringNullEmptyData
{
    public string? Name { get; set; }
}

// ------------------------------------------------------------
// Bracket and separator
// ------------------------------------------------------------

[GenerateToString(Bracket = ToStringBracket.Parenthesis, InnerSpace = ToStringSpace.None, TypeNameSpace = ToStringSpace.None)]
public partial class ToStringParenData
{
    public int Id { get; set; }

    public string Name { get; set; } = default!;
}

[GenerateToString(Bracket = ToStringBracket.Square, InnerSpace = ToStringSpace.None)]
public partial class ToStringSquareData
{
    public int Id { get; set; }
}

[GenerateToString(Bracket = ToStringBracket.Parenthesis)]
public partial class ToStringEmptyParenData;

[GenerateToString(TypeName = ToStringTypeName.None)]
public partial class ToStringNoTypeNameData
{
    public int Id { get; set; }
}

[GenerateToString(TypeName = ToStringTypeName.None, Bracket = ToStringBracket.None)]
public partial class ToStringNoBracketData
{
    public int Id { get; set; }

    public string Name { get; set; } = default!;
}

[GenerateToString(OpenBracket = "<<", CloseBracket = ">>", Separator = " | ", Assign = ":")]
public partial class ToStringCustomBracketData
{
    public int Id { get; set; }

    public string Name { get; set; } = default!;
}

// ------------------------------------------------------------
// Collection
// ------------------------------------------------------------

#pragma warning disable CA1819
[GenerateToString(CollectionLimit = 3)]
public partial class ToStringCollectionLimitData
{
    public int[]? Values { get; set; }
}

[GenerateToString(
    CollectionBracket = ToStringBracket.Parenthesis,
    CollectionInnerSpace = ToStringSpace.Space,
    CollectionSeparator = " / ")]
public partial class ToStringCollectionStyleData
{
    public int[]? Values { get; set; }
}
#pragma warning restore CA1819

// ------------------------------------------------------------
// Member attribute
// ------------------------------------------------------------

[GenerateToString]
public partial class ToStringMaskData
{
    [ToStringMask]
    public string Password { get; set; } = default!;

    [ToStringMask(Show = 2)]
    public string Token { get; set; } = default!;
}

#pragma warning disable CA1819
[GenerateToString]
public partial class ToStringMaskedCollectionData
{
    [ToStringMask]
    public string[]? Secrets { get; set; }

    public int[]? Values { get; set; }
}
#pragma warning restore CA1819

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
    [ToStringMaxLength(3)]
    public string Description { get; set; } = default!;

    public string Name { get; set; } = default!;
}

[GenerateToString]
public partial class ToStringFormatMaxLengthData
{
    [ToStringFormat("000000")]
    [ToStringMaxLength(3)]
    public int Number { get; set; }
}

public class ToStringTest
{
    // ------------------------------------------------------------
    // Default style
    // ------------------------------------------------------------

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
    public void TestNoTypeArgument()
    {
        // Arrange
        var data = new ToStringNoTypeArgumentData<int> { Value = 1 };

        // Act
        var text = data.ToString();

        // Assert
        Assert.Equal("ToStringNoTypeArgumentData { Value = 1 }", text);
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
    public void TestFullTypeName()
    {
        // Arrange
        var data = new ToStringOuterData.FullNameData { Id = 1 };

        // Act
        var text = data.ToString();

        // Assert
        Assert.Equal("BunnyTail.CommonCode.ToStringOuterData.FullNameData { Id = 1 }", text);
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
    public void TestPropertyOnly()
    {
        // Arrange
        var data = new ToStringPropertyOnlyData { Id = 1, Extra = 2 };

        // Act
        var text = data.ToString();

        // Assert
        Assert.Equal("ToStringPropertyOnlyData { Id = 1 }", text); // Fields are excluded by default
    }

    [Fact]
    public void TestField()
    {
        // Arrange
        var data = new ToStringFieldData { Id = 1, Extra = 2, Ignore = 3 };

        // Act
        var text = data.ToString();

        // Assert
        Assert.Equal("ToStringFieldData { Id = 1, Extra = 2 }", text); // Fields are included by Members
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

    // ------------------------------------------------------------
    // Record style
    // ------------------------------------------------------------

    [Fact]
    public void TestRecordCompatible()
    {
        // Arrange
        var withValues = new ToStringRecordEquivalent { Id = 1, Name = "xyz", Number = 5 };
        var withNulls = new ToStringRecordEquivalent { Id = 1 };
        var generatedWithValues = new ToStringRecordCompatData { Id = 1, Name = "xyz", Number = 5 };
        var generatedWithNulls = new ToStringRecordCompatData { Id = 1 };

        // Act
        var expectedWithValues = ToCompatText(withValues.ToString());
        var expectedWithNulls = ToCompatText(withNulls.ToString());

        // Assert
        // The output of the record style is identical to the record output
        Assert.Equal(expectedWithValues, generatedWithValues.ToString());
        Assert.Equal(expectedWithNulls, generatedWithNulls.ToString());

        static string ToCompatText(string text) =>
            text.Replace(nameof(ToStringRecordEquivalent), nameof(ToStringRecordCompatData), StringComparison.Ordinal);
    }

    [Fact]
    public void TestRecordStyle()
    {
        // Arrange
        var withValues = new ToStringRecordStyleData { Name = "xyz", Values = [1, 2] };
        var withNulls = new ToStringRecordStyleData();

        // Act
        var withValuesText = withValues.ToString();
        var withNullsText = withNulls.ToString();

        // Assert
        // The collection is not expanded and null becomes an empty string
        Assert.Equal("ToStringRecordStyleData { Name = xyz, Values = System.Int32[] }", withValuesText);
        Assert.Equal("ToStringRecordStyleData { Name = , Values =  }", withNullsText);
    }

    // ------------------------------------------------------------
    // Null
    // ------------------------------------------------------------

    [Fact]
    public void TestNullLiteral()
    {
        // Arrange
        var data = new ToStringNullLiteralData();

        // Act
        var text = data.ToString();

        // Assert
        Assert.Equal("ToStringNullLiteralData { Name = <null> }", text);
    }

    [Fact]
    public void TestNullEmpty()
    {
        // Arrange
        var data = new ToStringNullEmptyData();

        // Act
        var text = data.ToString();

        // Assert
        Assert.Equal("ToStringNullEmptyData { Name =  }", text);
    }

    // ------------------------------------------------------------
    // Bracket and separator
    // ------------------------------------------------------------

    [Fact]
    public void TestParenthesisBracket()
    {
        // Arrange
        var data = new ToStringParenData { Id = 1, Name = "x" };

        // Act
        var text = data.ToString();

        // Assert
        Assert.Equal("ToStringParenData(Id = 1, Name = x)", text);
    }

    [Fact]
    public void TestSquareBracket()
    {
        // Arrange
        var data = new ToStringSquareData { Id = 1 };

        // Act
        var text = data.ToString();

        // Assert
        Assert.Equal("ToStringSquareData [Id = 1]", text);
    }

    [Fact]
    public void TestEmptyWithBracket()
    {
        // Arrange
        var data = new ToStringEmptyParenData();

        // Act
        var text = data.ToString();

        // Assert
        Assert.Equal("ToStringEmptyParenData ( )", text);
    }

    [Fact]
    public void TestNoTypeName()
    {
        // Arrange
        var data = new ToStringNoTypeNameData { Id = 1 };

        // Act
        var text = data.ToString();

        // Assert
        Assert.Equal("{ Id = 1 }", text); // The space before the bracket is also suppressed
    }

    [Fact]
    public void TestNoBracket()
    {
        // Arrange
        var data = new ToStringNoBracketData { Id = 1, Name = "x" };

        // Act
        var text = data.ToString();

        // Assert
        Assert.Equal("Id = 1, Name = x", text); // The inner space is also suppressed
    }

    [Fact]
    public void TestCustomBracket()
    {
        // Arrange
        var data = new ToStringCustomBracketData { Id = 1, Name = "x" };

        // Act
        var text = data.ToString();

        // Assert
        Assert.Equal("ToStringCustomBracketData << Id:1 | Name:x >>", text);
    }

    // ------------------------------------------------------------
    // Collection
    // ------------------------------------------------------------

    [Fact]
    public void TestCollectionLimit()
    {
        // Arrange
        var over = new ToStringCollectionLimitData { Values = [1, 2, 3, 4, 5] };
        var just = new ToStringCollectionLimitData { Values = [1, 2, 3] };
        var under = new ToStringCollectionLimitData { Values = [1, 2] };

        // Act
        var overText = over.ToString();
        var justText = just.ToString();
        var underText = under.ToString();

        // Assert
        Assert.Equal("ToStringCollectionLimitData { Values = [1, 2, 3, ...] }", overText);
        Assert.Equal("ToStringCollectionLimitData { Values = [1, 2, 3] }", justText);
        Assert.Equal("ToStringCollectionLimitData { Values = [1, 2] }", underText);
    }

    [Fact]
    public void TestCollectionStyle()
    {
        // Arrange
        var withValues = new ToStringCollectionStyleData { Values = [1, 2] };
        var withEmpty = new ToStringCollectionStyleData { Values = [] };

        // Act
        var withValuesText = withValues.ToString();
        var withEmptyText = withEmpty.ToString();

        // Assert
        Assert.Equal("ToStringCollectionStyleData { Values = ( 1 / 2 ) }", withValuesText);
        Assert.Equal("ToStringCollectionStyleData { Values = () }", withEmptyText); // The inner space is not output for an empty collection
    }

    // ------------------------------------------------------------
    // Member attribute
    // ------------------------------------------------------------

    [Fact]
    public void TestMask()
    {
        // Arrange
        var masked = new ToStringMaskData { Password = "secret", Token = "abcd1234" };
        var shortValue = new ToStringMaskData { Password = "x", Token = "ab" };
        var nullValue = new ToStringMaskData();

        // Act
        var maskedText = masked.ToString();
        var shortText = shortValue.ToString();
        var nullText = nullValue.ToString();

        // Assert
        Assert.Equal("ToStringMaskData { Password = ***, Token = ***34 }", maskedText); // Shows only the trailing characters specified by Show
        Assert.Equal("ToStringMaskData { Password = ***, Token = *** }", shortText);    // For lengths up to Show, the tail is hidden and only *** is shown
        Assert.Equal("ToStringMaskData { Password = null, Token = null }", nullText);   // null is not masked and follows the null setting
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
