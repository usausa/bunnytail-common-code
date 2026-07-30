namespace BunnyTail.CommonCode;

// CommonCodeGeneratorToStringStyle is set to Record for this project

public record RecordEquivalent
{
    public int Id { get; init; }

    public string? Name { get; init; }

    public int? Number { get; init; }
}

// ReSharper disable once PartialTypeWithSinglePart
[GenerateToString]
public partial class RecordCompatData
{
    public int Id { get; init; }

    public string? Name { get; init; }

    public int? Number { get; init; }
}

#pragma warning disable CA1051
#pragma warning disable SA1401
public record RecordFieldEquivalent
{
    public int Id { get; init; }

    public int Extra;
}

// ReSharper disable once PartialTypeWithSinglePart
[GenerateToString]
public partial class RecordFieldCompatData
{
    public int Id { get; init; }

    public int Extra;
}
#pragma warning restore SA1401
#pragma warning restore CA1051

#pragma warning disable CA1819
public record RecordCollectionEquivalent
{
    public int[]? Values { get; init; }
}

// ReSharper disable once PartialTypeWithSinglePart
[GenerateToString]
public partial class RecordCollectionCompatData
{
    public int[]? Values { get; init; }
}
#pragma warning restore CA1819

public record RecordGenericEquivalent<T>
{
    public T Value { get; init; } = default!;
}

// ReSharper disable once PartialTypeWithSinglePart
[GenerateToString]
public partial class RecordGenericCompatData<T>
{
    public T Value { get; init; } = default!;
}

public record RecordEmptyEquivalent;

// ReSharper disable once PartialTypeWithSinglePart
[GenerateToString]
public partial class RecordEmptyCompatData;

[GenerateToString]
public partial class RecordMaskData
{
    [ToStringFormat(MaskPattern = "***")]
    public string? Password { get; set; }
}

public class ToStringRecordStyleTest
{
    [Fact]
    public void TestScalar()
    {
        // Arrange
        var withValues = new RecordEquivalent { Id = 1, Name = "xyz", Number = 5 };
        var withNulls = new RecordEquivalent { Id = 1 };

        // Act & Assert
        Assert.Equal(Expected(withValues.ToString()), new RecordCompatData { Id = 1, Name = "xyz", Number = 5 }.ToString());
        Assert.Equal(Expected(withNulls.ToString()), new RecordCompatData { Id = 1 }.ToString());

        static string Expected(string text) =>
            text.Replace(nameof(RecordEquivalent), nameof(RecordCompatData), StringComparison.Ordinal);
    }

    [Fact]
    public void TestField()
    {
        // Arrange
        var expected = new RecordFieldEquivalent { Id = 1, Extra = 2 };

        // Act
        var text = new RecordFieldCompatData { Id = 1, Extra = 2 }.ToString();

        // Assert
        // Public fields are output in the record style
        Assert.Equal(expected.ToString().Replace(nameof(RecordFieldEquivalent), nameof(RecordFieldCompatData), StringComparison.Ordinal), text);
        Assert.Equal("RecordFieldCompatData { Id = 1, Extra = 2 }", text);
    }

    [Fact]
    public void TestCollection()
    {
        // Arrange
        var withValues = new RecordCollectionEquivalent { Values = [1, 2] };
        var withNulls = new RecordCollectionEquivalent();

        // Act & Assert
        // The collection is not expanded and null becomes an empty string
        Assert.Equal(Expected(withValues.ToString()), new RecordCollectionCompatData { Values = [1, 2] }.ToString());
        Assert.Equal(Expected(withNulls.ToString()), new RecordCollectionCompatData().ToString());
        Assert.Equal("RecordCollectionCompatData { Values = System.Int32[] }", new RecordCollectionCompatData { Values = [1, 2] }.ToString());
        Assert.Equal("RecordCollectionCompatData { Values =  }", new RecordCollectionCompatData().ToString());

        static string Expected(string text) =>
            text.Replace(nameof(RecordCollectionEquivalent), nameof(RecordCollectionCompatData), StringComparison.Ordinal);
    }

    [Fact]
    public void TestGeneric()
    {
        // Arrange
        var expected = new RecordGenericEquivalent<int> { Value = 1 };

        // Act
        var text = new RecordGenericCompatData<int> { Value = 1 }.ToString();

        // Assert
        // The type name has no type arguments in the record style
        Assert.Equal(expected.ToString().Replace("RecordGenericEquivalent", "RecordGenericCompatData", StringComparison.Ordinal), text);
        Assert.Equal("RecordGenericCompatData { Value = 1 }", text);
    }

    [Fact]
    public void TestEmpty()
    {
        // Arrange
        var expected = new RecordEmptyEquivalent();

        // Act
        var text = new RecordEmptyCompatData().ToString();

        // Assert
        Assert.Equal(expected.ToString().Replace(nameof(RecordEmptyEquivalent), nameof(RecordEmptyCompatData), StringComparison.Ordinal), text);
        Assert.Equal("RecordEmptyCompatData { }", text);
    }

    [Fact]
    public void TestMask()
    {
        // Arrange
        var masked = new RecordMaskData { Password = "secret" };
        var nullValue = new RecordMaskData();

        // Act
        var maskedText = masked.ToString();
        var nullText = nullValue.ToString();

        // Assert
        Assert.Equal("RecordMaskData { Password = *** }", maskedText);
        Assert.Equal("RecordMaskData { Password =  }", nullText); // null follows the record style and becomes an empty string
    }
}
