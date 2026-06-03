namespace BunnyTail.CommonCode;

[GenerateEquality]
public partial class EqualityMoneyData
{
    public decimal Amount { get; init; }
    public string Currency { get; init; } = default!;

    [IgnoreEquality]
    public DateTime CapturedAt { get; init; }
}

[GenerateEquality(GenerateOperators = true, DeepCollectionEquality = true)]
public sealed partial class EqualityTaggedData
{
#pragma warning disable CA1819
    public string Name { get; init; } = default!;
    public string[] Tags { get; init; } = [];
#pragma warning restore CA1819
}

[GenerateEquality]
public partial class EqualityBaseAccount
{
    public int Id { get; init; }
}

[GenerateEquality(CallBase = true)]
public partial class EqualityMemberAccount : EqualityBaseAccount
{
    public string Name { get; init; } = default!;
}

public class EqualityTest
{
    [Fact]
    public void WhenSameValuesThenEquals()
    {
        var a = new EqualityMoneyData { Amount = 1.5m, Currency = "USD" };
        var b = new EqualityMoneyData { Amount = 1.5m, Currency = "USD" };
        Assert.True(a.Equals(b));
    }

    [Fact]
    public void WhenDifferentValuesThenNotEquals()
    {
        var a = new EqualityMoneyData { Amount = 1.5m, Currency = "USD" };
        var b = new EqualityMoneyData { Amount = 2.0m, Currency = "USD" };
        Assert.False(a.Equals(b));
    }

    [Fact]
    public void WhenIgnoredPropertyDiffersThenEquals()
    {
        var a = new EqualityMoneyData { Amount = 1m, Currency = "USD", CapturedAt = DateTime.MinValue };
        var b = new EqualityMoneyData { Amount = 1m, Currency = "USD", CapturedAt = DateTime.MaxValue };
        Assert.True(a.Equals(b));
    }

    [Fact]
    public void WhenSameReferenceThenEquals()
    {
        var a = new EqualityMoneyData { Amount = 1m, Currency = "USD" };
        Assert.True(a.Equals(a));
    }

    [Fact]
    public void GetHashCodeConsistentWithEquals()
    {
        var a = new EqualityMoneyData { Amount = 1.5m, Currency = "USD" };
        var b = new EqualityMoneyData { Amount = 1.5m, Currency = "USD" };
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void OperatorEqualWhenSameValues()
    {
        var a = new EqualityTaggedData { Name = "x", Tags = ["a", "b"] };
        var b = new EqualityTaggedData { Name = "x", Tags = ["a", "b"] };
        Assert.True(a == b);
        Assert.False(a != b);
    }

    [Fact]
    public void OperatorNotEqualWhenDifferentTags()
    {
        var a = new EqualityTaggedData { Name = "x", Tags = ["a"] };
        var b = new EqualityTaggedData { Name = "x", Tags = ["b"] };
        Assert.True(a != b);
    }

    [Fact]
    public void DeepCollectionEqualityWithNullTags()
    {
        var a = new EqualityTaggedData { Name = "x", Tags = null! };
        var b = new EqualityTaggedData { Name = "x", Tags = null! };
        Assert.True(a == b);
    }

    [Fact]
    public void WhenCallBaseWithInheritanceThenComparesBaseAndDerived()
    {
        var a = new EqualityMemberAccount { Id = 1, Name = "Alice" };
        var b = new EqualityMemberAccount { Id = 1, Name = "Alice" };
        var differentBase = new EqualityMemberAccount { Id = 2, Name = "Alice" };
        var differentDerived = new EqualityMemberAccount { Id = 1, Name = "Bob" };

        Assert.True(a.Equals(b));
        Assert.False(a.Equals(differentBase));    // 基底クラスのプロパティ差分を base.Equals で検出
        Assert.False(a.Equals(differentDerived)); // 派生クラスのプロパティ差分を検出
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }
}
