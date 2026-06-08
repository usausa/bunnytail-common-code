namespace BunnyTail.CommonCode;

#pragma warning disable CA1819
// ReSharper disable once PartialTypeWithSinglePart
[GenerateToString]
public partial class ToStringData
{
    public int Id { get; set; }

    public string Name { get; set; } = default!;

    public int[] IntValues { get; set; } = default!;

    public string?[] StringValues { get; set; } = default!;

    [IgnoreToString]
    public int Ignore { get; set; }
}
#pragma warning restore CA1819

// ReSharper disable once PartialTypeWithSinglePart
[GenerateToString]
public partial class ToStringGenericData<T>
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

[GenerateToString]
public partial class ToStringMaskData
{
    [ToStringMask]
    public string Password { get; set; } = default!;

    [ToStringMask(Show = 2)]
    public string Token { get; set; } = default!;
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
    [Fact]
    public void TestBasic()
    {
        // Arrange
        var withValues = new ToStringData { Id = 123, Name = "xyz", IntValues = [1, 2], StringValues = ["a", null] };
        var withNulls = new ToStringData { Id = 123, Name = "xyz" };

        // Act
        var withValuesText = withValues.ToString();
        var withNullsText = withNulls.ToString();

        // Assert
        Assert.Equal("{ Id = 123, Name = xyz, IntValues = [1, 2], StringValues = [a, null] }", withValuesText); // Array elements are expanded and null is also output
        Assert.Equal("{ Id = 123, Name = xyz, IntValues = null, StringValues = null }", withNullsText);          // A null array is output as null
    }

    [Fact]
    public void TestGeneric()
    {
        // Arrange
        var intData = new ToStringGenericData<int> { Value = 123 };
        var stringData = new ToStringGenericData<string> { Value = "xyz" };
        var nullData = new ToStringGenericData<string?> { Value = null };

        // Act
        var intText = intData.ToString();
        var stringText = stringData.ToString();
        var nullText = nullData.ToString();

        // Assert
        Assert.Equal("{ Value = 123 }", intText);
        Assert.Equal("{ Value = xyz }", stringText);
        Assert.Equal("{ Value = null }", nullText);
    }

    [Fact]
    public void TestInnerClass()
    {
        // Arrange
        var data = new ToStringOuterData.InnerData { Id = 456, Name = "inner" };

        // Act
        var text = data.ToString();

        // Assert
        Assert.Equal("{ Id = 456, Name = inner }", text); // Generated even for nested types
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
        Assert.Equal("{ Value = x }", text);
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
        Assert.Equal("{ Name = x }", text); // The indexer is excluded from output
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
        Assert.Equal("{ Label = L }", text);
    }

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
        Assert.Equal("{ Password = ***, Token = ***34 }", maskedText); // Shows only the trailing characters specified by Show
        Assert.Equal("{ Password = ***, Token = *** }", shortText);    // For lengths up to Show, the tail is hidden and only *** is shown
        Assert.Equal("{ Password = null, Token = null }", nullText);   // null is not masked and is output as null
    }

    [Fact]
    public void TestFormat()
    {
        // Arrange
        var data = new ToStringFormatData { Code = 7, Hex = 255 };

        // Act
        var text = data.ToString();

        // Assert
        Assert.Equal("{ Code = 007, Hex = 00FF }", text); // The ToStringFormat format is applied
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
        Assert.Equal("{ Description = abc, Name = x }", longText);     // Truncated to the maximum length
        Assert.Equal("{ Description = ab, Name = y }", withinText);    // Within the limit, kept as is
        Assert.Equal("{ Description = null, Name = z }", nullText);    // null is excluded from truncation
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
        Assert.Equal("{ Number = 000 }", text);
    }
}
