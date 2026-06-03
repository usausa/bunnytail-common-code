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
        var a = new CompareToPersonData { LastName = "Doe", FirstName = "John" };
        var b = new CompareToPersonData { LastName = "Doe", FirstName = "John" };
        Assert.Equal(0, ((IComparable<CompareToPersonData>)a).CompareTo(b));
    }

    [Fact]
    public void WhenFirstKeyDifferentThenCompareByFirst()
    {
        var a = new CompareToPersonData { LastName = "Adams", FirstName = "John" };
        var b = new CompareToPersonData { LastName = "Doe", FirstName = "John" };
        Assert.True(((IComparable<CompareToPersonData>)a).CompareTo(b) < 0);
        Assert.True(((IComparable<CompareToPersonData>)b).CompareTo(a) > 0);
    }

    [Fact]
    public void WhenFirstKeySameThenCompareBySecond()
    {
        var a = new CompareToPersonData { LastName = "Doe", FirstName = "Alice" };
        var b = new CompareToPersonData { LastName = "Doe", FirstName = "Bob" };
        Assert.True(((IComparable<CompareToPersonData>)a).CompareTo(b) < 0);
    }

    [Fact]
    public void WhenComparedToNullThenPositive()
    {
        var a = new CompareToPersonData { LastName = "Doe", FirstName = "John" };
        Assert.True(((IComparable<CompareToPersonData>)a).CompareTo(null) > 0);
    }

    [Fact]
    public void NonKeyPropertyIgnoredInComparison()
    {
        var a = new CompareToPersonData { LastName = "Doe", FirstName = "John", Age = 20 };
        var b = new CompareToPersonData { LastName = "Doe", FirstName = "John", Age = 40 };
        Assert.Equal(0, ((IComparable<CompareToPersonData>)a).CompareTo(b));
    }

    [Fact]
    public void OperatorLessThan()
    {
        var a = new CompareToPersonData { LastName = "Adams", FirstName = "A" };
        var b = new CompareToPersonData { LastName = "Zorn", FirstName = "Z" };
        Assert.True(a < b);
        Assert.False(b < a);
    }

    [Fact]
    public void OperatorGreaterThan()
    {
        var a = new CompareToPersonData { LastName = "Zorn", FirstName = "Z" };
        var b = new CompareToPersonData { LastName = "Adams", FirstName = "A" };
        Assert.True(a > b);
    }

    [Fact]
    public void OperatorLessThanOrEqual()
    {
        var a = new CompareToPersonData { LastName = "Doe", FirstName = "John" };
        var b = new CompareToPersonData { LastName = "Doe", FirstName = "John" };
        Assert.True(a <= b);
    }

    [Fact]
    public void OperatorGreaterThanOrEqual()
    {
        var a = new CompareToPersonData { LastName = "Doe", FirstName = "John" };
        var b = new CompareToPersonData { LastName = "Doe", FirstName = "John" };
        Assert.True(a >= b);
    }
}
