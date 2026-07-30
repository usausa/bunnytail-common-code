namespace BunnyTail.CommonCode.Options;

// Individual MSBuild options are set for this project, see the csproj for the values

#pragma warning disable CA1819
#pragma warning disable CA1051
#pragma warning disable SA1401
// ReSharper disable once PartialTypeWithSinglePart
[GenerateToString]
public partial class OptionData
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public int[]? Values { get; set; }

    public int Extra;
}
#pragma warning restore SA1401
#pragma warning restore CA1051
#pragma warning restore CA1819

// ReSharper disable once PartialTypeWithSinglePart
[GenerateToString]
public partial class OptionGenericData<T>
{
    public T Value { get; set; } = default!;
}

// ReSharper disable once PartialTypeWithSinglePart
[GenerateToString]
public partial class OptionEmptyData;

public static partial class OptionOuterData
{
    [GenerateToString]
    public partial class InnerData
    {
        public int Id { get; set; }
    }
}

public class ToStringOptionsTest
{
    [Fact]
    public void TestFullTypeNameAndBracket()
    {
        // Arrange
        var data = new OptionData { Id = 1, Name = "x", Values = [1, 2], Extra = 9 };

        // Act
        var text = data.ToString();

        // Assert
        // TypeName=Full, TypeNameSpace=None, OpenBracket=<< takes precedence over Bracket=Parenthesis,
        // CloseBracket falls back to Bracket=Parenthesis, InnerSpace=None, Assign=:,
        // Separator is quoted in the csproj so that the surrounding spaces are kept,
        // Members=PropertyAndField so Extra is output
        Assert.Equal("BunnyTail.CommonCode.Options.OptionData<<Id:1 | Name:x | Values:< 1/2 > | Extra:9)", text);
    }

    [Fact]
    public void TestNullLiteral()
    {
        // Arrange
        var data = new OptionData { Id = 1, Extra = 9 };

        // Act
        var text = data.ToString();

        // Assert
        Assert.Equal("BunnyTail.CommonCode.Options.OptionData<<Id:1 | Name:<null> | Values:<null> | Extra:9)", text);
    }

    [Fact]
    public void TestCollectionLimit()
    {
        // Arrange
        var over = new OptionData { Values = [1, 2, 3, 4] };
        var empty = new OptionData { Values = [] };

        // Act
        var overText = over.ToString();
        var emptyText = empty.ToString();

        // Assert
        Assert.Equal("BunnyTail.CommonCode.Options.OptionData<<Id:0 | Name:<null> | Values:< 1/2/... > | Extra:0)", overText);
        // The collection inner space is not output for an empty collection
        Assert.Equal("BunnyTail.CommonCode.Options.OptionData<<Id:0 | Name:<null> | Values:<> | Extra:0)", emptyText);
    }

    [Fact]
    public void TestNoTypeArgument()
    {
        // Arrange
        var data = new OptionGenericData<int> { Value = 1 };

        // Act
        var text = data.ToString();

        // Assert
        // TypeArgument=None so the type arguments are not output
        Assert.Equal("BunnyTail.CommonCode.Options.OptionGenericData<<Value:1)", text);
    }

    [Fact]
    public void TestEmpty()
    {
        // Arrange
        var data = new OptionEmptyData();

        // Act
        var text = data.ToString();

        // Assert
        Assert.Equal("BunnyTail.CommonCode.Options.OptionEmptyData<<)", text);
    }

    [Fact]
    public void TestFullTypeNameOfInnerClass()
    {
        // Arrange
        var data = new OptionOuterData.InnerData { Id = 1 };

        // Act
        var text = data.ToString();

        // Assert
        // The containing types are included in the full type name
        Assert.Equal("BunnyTail.CommonCode.Options.OptionOuterData.InnerData<<Id:1)", text);
    }
}
