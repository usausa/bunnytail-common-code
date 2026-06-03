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

[GenerateEquality]
public partial class EqualityMemberAccount : EqualityBaseAccount
{
    public string Name { get; init; } = default!;
}

public class EqualityShadowBase
{
    public int Value { get; init; }
}

// 派生型が基底プロパティを new で隠蔽 (型も変更) しても、最派生の宣言だけを比較する
[GenerateEquality]
public partial class EqualityShadowDerived : EqualityShadowBase
{
    public new string Value { get; init; } = default!;
}

// インデクサを持つ型でも、インデクサは比較対象外で通常プロパティのみ比較する
[GenerateEquality]
public partial class EqualityIndexedData
{
    private readonly Dictionary<string, string> map = [];

    public string Name { get; init; } = default!;

    public string this[string key]
    {
        get => this.map[key];
        set => this.map[key] = value;
    }
}

public class EqualityHiddenBase
{
    public string Token { get; init; } = default!;
}

// 派生が public new(別型) を IgnoreEquality で隠蔽。
// this.Token は派生(int)に束縛され基底 public(string) に到達できないため、Token は比較対象外 (仕様: 到達可能な最派生のみ対象)。
[GenerateEquality]
public partial class EqualityHiddenDerived : EqualityHiddenBase
{
    [IgnoreEquality]
    public new int Token { get; init; }

    public string Label { get; init; } = default!;
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
    public void WhenInheritedThenComparesOwnAndInheritedMembers()
    {
        var a = new EqualityMemberAccount { Id = 1, Name = "Alice" };
        var b = new EqualityMemberAccount { Id = 1, Name = "Alice" };
        var differentInherited = new EqualityMemberAccount { Id = 2, Name = "Alice" };
        var differentOwn = new EqualityMemberAccount { Id = 1, Name = "Bob" };

        Assert.True(a.Equals(b));
        Assert.False(a.Equals(differentInherited)); // 継承した Id の差分を検出 (フラット比較)
        Assert.False(a.Equals(differentOwn));       // 自型 Name の差分を検出
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void WhenBasePropertyShadowedThenUsesDerivedDeclaration()
    {
        var a = new EqualityShadowDerived { Value = "x" };
        var b = new EqualityShadowDerived { Value = "x" };
        var c = new EqualityShadowDerived { Value = "y" };

        Assert.True(a.Equals(b));
        Assert.False(a.Equals(c));
    }

    [Fact]
    public void WhenTypeHasIndexerThenIndexerIsExcluded()
    {
        var a = new EqualityIndexedData { Name = "x" };
        var b = new EqualityIndexedData { Name = "x" };
        a["k"] = "1";
        b["k"] = "2"; // インデクサの内容が違っても比較対象外

        Assert.True(a.Equals(b));
    }

    [Fact]
    public void WhenBasePublicHiddenByIgnoredNewThenNameIsExcluded()
    {
        // この型がコンパイルできること自体が回帰防止 (採用後登録に変えると EqualityComparer<string> へ int を渡しコンパイル不能)
        var a = new EqualityHiddenDerived { Token = 1, Label = "L" };
        var b = new EqualityHiddenDerived { Token = 2, Label = "L" };
        var c = new EqualityHiddenDerived { Token = 1, Label = "M" };

        Assert.True(a.Equals(b));  // 隠蔽された Token は比較対象外
        Assert.False(a.Equals(c)); // Label で判定
    }
}
