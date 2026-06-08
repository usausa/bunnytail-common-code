namespace BunnyTail.CommonCode;

#pragma warning disable CA1036
[GenerateCompareTo]
public partial class CompareToPersonData
{
    [CompareKey(Order = 1)]
    public string LastName { get; init; } = default!;

    [CompareKey(Order = 2)]
    public string FirstName { get; init; } = default!;

    public int Age { get; init; }
}
#pragma warning restore CA1036

public class CompareToTest
{
    [Fact]
    public void WhenSameKeysThenZero()
    {
        // Arrange
        var a = new CompareToPersonData { LastName = "Doe", FirstName = "John" };
        var b = new CompareToPersonData { LastName = "Doe", FirstName = "John" };

        // Act
        var result = ((IComparable<CompareToPersonData>)a).CompareTo(b);

        // Assert
        Assert.Equal(0, result); // All keys are equal, so 0
    }

    [Fact]
    public void WhenFirstKeyDifferentThenCompareByFirst()
    {
        // Arrange
        var a = new CompareToPersonData { LastName = "Adams", FirstName = "John" };
        var b = new CompareToPersonData { LastName = "Doe", FirstName = "John" };

        // Act
        var forward = ((IComparable<CompareToPersonData>)a).CompareTo(b);
        var backward = ((IComparable<CompareToPersonData>)b).CompareTo(a);

        // Assert
        Assert.True(forward < 0);   // Order is determined by the first key LastName
        Assert.True(backward > 0);  // The sign is reversed in the opposite direction
    }

    [Fact]
    public void WhenFirstKeySameThenCompareBySecond()
    {
        // Arrange
        var a = new CompareToPersonData { LastName = "Doe", FirstName = "Alice" };
        var b = new CompareToPersonData { LastName = "Doe", FirstName = "Bob" };

        // Act
        var result = ((IComparable<CompareToPersonData>)a).CompareTo(b);

        // Assert
        Assert.True(result < 0); // When the first key is equal, compare by the second key FirstName
    }

    [Fact]
    public void WhenComparedToNullThenPositive()
    {
        // Arrange
        var a = new CompareToPersonData { LastName = "Doe", FirstName = "John" };

        // Act
        var result = ((IComparable<CompareToPersonData>)a).CompareTo(null);

        // Assert
        Assert.True(result > 0); // Treated as greater than null
    }

    [Fact]
    public void NonKeyPropertyIgnoredInComparison()
    {
        // Arrange
        var a = new CompareToPersonData { LastName = "Doe", FirstName = "John", Age = 20 };
        var b = new CompareToPersonData { LastName = "Doe", FirstName = "John", Age = 40 };

        // Act
        var result = ((IComparable<CompareToPersonData>)a).CompareTo(b);

        // Assert
        Assert.Equal(0, result); // Age, which is not a CompareKey, is excluded from comparison
    }

    [Fact]
    public void OperatorLessThan()
    {
        // Arrange
        var a = new CompareToPersonData { LastName = "Adams", FirstName = "A" };
        var b = new CompareToPersonData { LastName = "Zorn", FirstName = "Z" };

        // Act & Assert
        Assert.True(a < b);
        Assert.False(b < a);
    }

    [Fact]
    public void OperatorGreaterThan()
    {
        // Arrange
        var a = new CompareToPersonData { LastName = "Zorn", FirstName = "Z" };
        var b = new CompareToPersonData { LastName = "Adams", FirstName = "A" };

        // Act
        var result = a > b;

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void OperatorLessThanOrEqual()
    {
        // Arrange
        var a = new CompareToPersonData { LastName = "Doe", FirstName = "John" };
        var b = new CompareToPersonData { LastName = "Doe", FirstName = "John" };

        // Act
        var result = a <= b;

        // Assert
        Assert.True(result); // True also when equal
    }

    [Fact]
    public void OperatorGreaterThanOrEqual()
    {
        // Arrange
        var a = new CompareToPersonData { LastName = "Doe", FirstName = "John" };
        var b = new CompareToPersonData { LastName = "Doe", FirstName = "John" };

        // Act
        var result = a >= b;

        // Assert
        Assert.True(result); // True also when equal
    }
}
