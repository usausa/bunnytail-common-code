namespace BunnyTail.CommonCode;

// The bracket is disabled and only the close side is given explicitly, see the csproj for the settings

#pragma warning disable CA1819
// ReSharper disable once PartialTypeWithSinglePart
[GenerateToString]
public partial class NoBracketData
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public int[]? Values { get; set; }
}
#pragma warning restore CA1819

// ReSharper disable once PartialTypeWithSinglePart
[GenerateToString]
public partial class NoBracketEmptyData;

public class ToStringNoBracketTests
{
    [Fact]
    public void TestNoTypeNameAndOpenBracket()
    {
        // Arrange
        var data = new NoBracketData { Id = 1, Name = "x", Values = [10, 20] };

        // Act
        var text = data.ToString();

        // Assert
        // TypeName=None removes the type name and the space that follows it,
        // CloseBracket takes precedence over Bracket=None so only the close side is written,
        // CollectionCloseBracket does the same for the expanded collection
        Assert.Equal("Id = 1, Name = x, Values = 10, 20|!", text);
    }

    [Fact]
    public void TestNull()
    {
        // Arrange
        var data = new NoBracketData();

        // Act
        var text = data.ToString();

        // Assert
        Assert.Equal("Id = 0, Name = null, Values = null!", text);
    }

    [Fact]
    public void TestEmptyCollection()
    {
        // Arrange
        var data = new NoBracketData { Id = 1, Name = "x", Values = [] };

        // Act
        var text = data.ToString();

        // Assert
        Assert.Equal("Id = 1, Name = x, Values = |!", text);
    }

    [Fact]
    public void TestEmptyType()
    {
        // Arrange
        var data = new NoBracketEmptyData();

        // Act
        var text = data.ToString();

        // Assert
        Assert.Equal("!", text);
    }
}
