namespace BunnyTail.CommonCode;

[GenerateEquality]
internal partial struct StructData
{
    public int Id { get; set; }
    public string Name { get; set; }
}

[GenerateEquality]
internal readonly partial struct ReadOnlyStructData
{
    public ReadOnlyStructData(int value, string label)
    {
        Value = value;
        Label = label;
    }

    public int Value { get; }
    public string Label { get; }
}

[GenerateEquality(DeepCollectionEquality = true)]
internal readonly partial struct CollectionStructData
{
    public List<int> Items { get; init; }
}

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

// Even when a derived type hides a base property with new (also changing the type), only the most-derived declaration is compared
[GenerateEquality]
public partial class EqualityShadowDerived : EqualityShadowBase
{
    public new string Value { get; init; } = default!;
}

// Even for a type with an indexer, the indexer is excluded and only regular properties are compared
[GenerateEquality]
public partial class EqualityIndexedData
{
    private readonly Dictionary<string, string> map = [];

    public string Name { get; init; } = default!;

    public string this[string key]
    {
        get => map[key];
        set => map[key] = value;
    }
}

public class EqualityHiddenBase
{
    public string Token { get; init; } = default!;
}

// The derived type hides the public member with new (different type) marked IgnoreEquality.
// Since this.Token binds to the derived one (int) and cannot reach the base public one (string), Token is excluded (spec: only the reachable most-derived member is targeted).
[GenerateEquality]
public partial class EqualityHiddenDerived : EqualityHiddenBase
{
    [IgnoreEquality]
    public new int Token { get; init; }

    public string Label { get; init; } = default!;
}

// record struct: generator must not generate any Equals — built-in value equality must be preserved.
[GenerateEquality]
internal partial record struct RecordStructData(int X);

[GenerateEquality(DeepCollectionEquality = true)]
internal sealed partial class EqualityEnumerableData
{
    public List<int> Values { get; init; } = [];
}

public class EqualityTest
{
    [Fact]
    public void WhenSameValuesThenEquals()
    {
        // Arrange
        var a = new EqualityMoneyData { Amount = 1.5m, Currency = "USD" };
        var b = new EqualityMoneyData { Amount = 1.5m, Currency = "USD" };

        // Act
        var result = a.Equals(b);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void WhenDifferentValuesThenNotEquals()
    {
        // Arrange
        var a = new EqualityMoneyData { Amount = 1.5m, Currency = "USD" };
        var b = new EqualityMoneyData { Amount = 2.0m, Currency = "USD" };

        // Act
        var result = a.Equals(b);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void WhenIgnoredPropertyDiffersThenEquals()
    {
        // Arrange
        var a = new EqualityMoneyData { Amount = 1m, Currency = "USD", CapturedAt = DateTime.MinValue };
        var b = new EqualityMoneyData { Amount = 1m, Currency = "USD", CapturedAt = DateTime.MaxValue };

        // Act
        var result = a.Equals(b);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void GetHashCodeConsistentWithEquals()
    {
        // Arrange
        var a = new EqualityMoneyData { Amount = 1.5m, Currency = "USD" };
        var b = new EqualityMoneyData { Amount = 1.5m, Currency = "USD" };

        // Act
        var hashA = a.GetHashCode();
        var hashB = b.GetHashCode();

        // Assert
        Assert.Equal(hashA, hashB);
    }

    [Fact]
    public void OperatorEqualWhenSameValues()
    {
        // Arrange
        var a = new EqualityTaggedData { Name = "x", Tags = ["a", "b"] };
        var b = new EqualityTaggedData { Name = "x", Tags = ["a", "b"] };

        // Act & Assert
        Assert.True(a == b);
        Assert.False(a != b);
    }

    [Fact]
    public void OperatorNotEqualWhenDifferentTags()
    {
        // Arrange
        var a = new EqualityTaggedData { Name = "x", Tags = ["a"] };
        var b = new EqualityTaggedData { Name = "x", Tags = ["b"] };

        // Act
        var result = a != b;

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void DeepCollectionEqualityWithNullTags()
    {
        // Arrange
        var a = new EqualityTaggedData { Name = "x", Tags = null! };
        var b = new EqualityTaggedData { Name = "x", Tags = null! };

        // Act
        var result = a == b;

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void WhenInheritedThenComparesOwnAndInheritedMembers()
    {
        // Arrange
        var a = new EqualityMemberAccount { Id = 1, Name = "Alice" };
        var b = new EqualityMemberAccount { Id = 1, Name = "Alice" };
        var differentInherited = new EqualityMemberAccount { Id = 2, Name = "Alice" };
        var differentOwn = new EqualityMemberAccount { Id = 1, Name = "Bob" };

        // Act & Assert
        Assert.True(a.Equals(b));
        Assert.False(a.Equals(differentInherited)); // Detects difference in inherited Id (flat comparison)
        Assert.False(a.Equals(differentOwn));       // Detects difference in own Name
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void WhenBasePropertyShadowedThenUsesDerivedDeclaration()
    {
        // Arrange
        var a = new EqualityShadowDerived { Value = "x" };
        var b = new EqualityShadowDerived { Value = "x" };
        var c = new EqualityShadowDerived { Value = "y" };

        // Act & Assert
        Assert.True(a.Equals(b));
        Assert.False(a.Equals(c));
    }

    [Fact]
    public void WhenTypeHasIndexerThenIndexerIsExcluded()
    {
        // Arrange
        var a = new EqualityIndexedData { Name = "x" };
        var b = new EqualityIndexedData { Name = "x" };
        a["k"] = "1";
        b["k"] = "2"; // Indexer content differs but is excluded from comparison

        // Act
        var result = a.Equals(b);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void WhenBasePublicHiddenByIgnoredNewThenNameIsExcluded()
    {
        // Arrange
        // The fact that this type compiles is itself a regression guard (changing to opt-in registration would pass int to EqualityComparer<string> and fail to compile)
        var a = new EqualityHiddenDerived { Token = 1, Label = "L" };
        var b = new EqualityHiddenDerived { Token = 2, Label = "L" };
        var c = new EqualityHiddenDerived { Token = 1, Label = "M" };

        // Act & Assert
        Assert.True(a.Equals(b));  // Hidden Token is excluded from comparison
        Assert.False(a.Equals(c)); // Determined by Label
    }

    [Fact]
    public void StructDataSameValuesThenEquals()
    {
        // Arrange
        var a = new StructData { Id = 1, Name = "Alice" };
        var b = new StructData { Id = 1, Name = "Alice" };

        // Act & Assert
        Assert.True(a.Equals(b));
        Assert.True(a == b);
        Assert.False(a != b);
    }

    [Fact]
    public void StructDataDifferentValuesThenNotEquals()
    {
        // Arrange
        var a = new StructData { Id = 1, Name = "Alice" };
        var b = new StructData { Id = 2, Name = "Alice" };

        // Act & Assert
        Assert.False(a.Equals(b));
        Assert.False(a == b);
        Assert.True(a != b);
    }

    [Fact]
    public void StructDataGetHashCodeConsistentWithEquals()
    {
        // Arrange
        var a = new StructData { Id = 42, Name = "Bob" };
        var b = new StructData { Id = 42, Name = "Bob" };

        // Act & Assert
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void StructDataEquatableInterfaceWorks()
    {
        // Arrange
        var a = new StructData { Id = 7, Name = "X" };
        var b = new StructData { Id = 7, Name = "X" };

        // Act & Assert — verify IEquatable<StructData> is callable via interface dispatch
        // Box through object so the static analyzer cannot infer the concrete type.
        object boxed = a;
        Assert.True(((System.IEquatable<StructData>)boxed).Equals(b));
    }

    [Fact]
    public void StructDataEqualsObjectWithDifferentTypeFalse()
    {
        // Arrange
        var a = new StructData { Id = 1, Name = "A" };

        // Act & Assert
        Assert.False(a.Equals(obj: "not a StructData"));
    }

    [Fact]
    public void ReadOnlyStructDataSameValuesThenEquals()
    {
        // Arrange
        var a = new ReadOnlyStructData(10, "hello");
        var b = new ReadOnlyStructData(10, "hello");

        // Act & Assert
        Assert.True(a.Equals(b));
        Assert.True(a == b);
    }

    [Fact]
    public void ReadOnlyStructDataDifferentValuesThenNotEquals()
    {
        // Arrange
        var a = new ReadOnlyStructData(10, "hello");
        var b = new ReadOnlyStructData(99, "hello");

        // Act & Assert
        Assert.False(a.Equals(b));
        Assert.False(a == b);
        Assert.True(a != b);
    }

    [Fact]
    public void CollectionStructDataSameElementsDifferentInstancesThenEquals()
    {
        // Arrange
        var a = new CollectionStructData { Items = [1, 2, 3] };
        var b = new CollectionStructData { Items = [1, 2, 3] };

        // Act & Assert — DeepCollectionEquality compares element-by-element
        Assert.True(a.Equals(b));
    }

    [Fact]
    public void RecordStructDataBuiltInEqualityPreserved()
    {
        // Arrange
        var a = new RecordStructData(5);
        var b = new RecordStructData(5);
        var c = new RecordStructData(6);

        // Act & Assert — built-in record struct equality, generator emits nothing for records
        Assert.True(a == b);
        Assert.True(a.Equals(b));
        Assert.False(a == c);
    }

    [Fact]
    public void EnumerableDataSameElementsDifferentListInstancesThenEquals()
    {
        // Arrange
        var a = new EqualityEnumerableData { Values = [10, 20, 30] };
        var b = new EqualityEnumerableData { Values = [10, 20, 30] };

        // Act & Assert — IsCollectionType fix: IEnumerable<T> itself is recognised as collection
        Assert.True(a.Equals(b));
    }
}
