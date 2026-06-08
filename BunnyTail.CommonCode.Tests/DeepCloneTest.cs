namespace BunnyTail.CommonCode;

[GenerateDeepClone]
public partial class DeepCloneAuthorData : IDeepCloneable<DeepCloneAuthorData>
{
    public string Name { get; set; } = default!;
}

#pragma warning disable CA1819
#pragma warning disable CA2227
[GenerateDeepClone]
public partial class DeepCloneDocumentData : IDeepCloneable<DeepCloneDocumentData>
{
    public string Title { get; set; } = default!;

    public List<string> Tags { get; set; } = [];

    public int[] Scores { get; set; } = [];

    public DeepCloneAuthorData Owner { get; set; } = default!;

    [ShallowClone]
    public object? ExtraRef { get; set; }

    [IgnoreClone]
    public int CacheKey { get; set; }
}
#pragma warning restore CA2227
#pragma warning restore CA1819

[GenerateDeepClone]
public partial class DeepCloneProfileData : IDeepCloneable<DeepCloneProfileData>
{
    // init-only: cloned via the object initializer
    public string DisplayName { get; init; } = default!;

    // settable: cloned via assignment
    public int Level { get; set; }

    // get-only (computed property): not assignable, so excluded from cloning
    public string Badge => $"{DisplayName}#{Level}";
}

// The indexer is excluded from cloning (cannot be assigned via clone.<Name>)
[GenerateDeepClone]
public partial class DeepCloneIndexedData : IDeepCloneable<DeepCloneIndexedData>
{
    private readonly Dictionary<string, string> map = [];

    public string Title { get; set; } = default!;

    public string this[string key]
    {
        get => map[key];
        set => map[key] = value;
    }
}

public class DeepCloneTest
{
    [Fact]
    public void WhenClonedThenIndependentCopy()
    {
        // Arrange
        var original = new DeepCloneDocumentData
        {
            Title = "Hello",
            Tags = ["a", "b"],
            Scores = [1, 2, 3],
            Owner = new DeepCloneAuthorData { Name = "Alice" },
            ExtraRef = new object(),
            CacheKey = 42
        };

        // Act
        var clone = original.DeepClone();

        // Assert
        Assert.Equal(original.Title, clone.Title);
        Assert.Equal(original.Tags, clone.Tags);
        Assert.Equal(original.Scores, clone.Scores);
        Assert.Equal(original.Owner.Name, clone.Owner.Name);
    }

    [Fact]
    public void WhenTagsModifiedThenOriginalUnchanged()
    {
        // Arrange
        var original = new DeepCloneDocumentData { Tags = ["a", "b"] };
        var clone = original.DeepClone();

        // Act
        clone.Tags.Add("c");

        // Assert
        Assert.Equal(2, original.Tags.Count); // Changes to the clone do not affect the original
        Assert.Equal(3, clone.Tags.Count);
    }

    [Fact]
    public void WhenScoresModifiedThenOriginalUnchanged()
    {
        // Arrange
        var original = new DeepCloneDocumentData { Scores = [1, 2] };
        var clone = original.DeepClone();

        // Act
        clone.Scores[0] = 99;

        // Assert
        Assert.Equal(1, original.Scores[0]); // Arrays are also cloned independently
    }

    [Fact]
    public void WhenOwnerModifiedThenOriginalUnchanged()
    {
        // Arrange
        var original = new DeepCloneDocumentData { Owner = new DeepCloneAuthorData { Name = "Alice" } };
        var clone = original.DeepClone();

        // Act
        clone.Owner.Name = "Bob";

        // Assert
        Assert.Equal("Alice", original.Owner.Name); // Nested reference types are also deeply cloned
    }

    [Fact]
    public void WhenShallowClonePropModifiedThenBothSeeChange()
    {
        // Arrange
        var sharedRef = new object();
        var original = new DeepCloneDocumentData { ExtraRef = sharedRef };

        // Act
        var clone = original.DeepClone();

        // Assert
        Assert.Same(sharedRef, clone.ExtraRef); // ShallowClone shares the reference
    }

    [Fact]
    public void WhenCloneIgnorePropThenNotCopied()
    {
        // Arrange
        var original = new DeepCloneDocumentData { CacheKey = 42 };

        // Act
        var clone = original.DeepClone();

        // Assert
        Assert.Equal(0, clone.CacheKey); // IgnoreClone is not cloned and stays at its default value
    }

    [Fact]
    public void WhenNullTagsThenNullInClone()
    {
        // Arrange
        var original = new DeepCloneDocumentData { Tags = null! };

        // Act
        var clone = original.DeepClone();

        // Assert
        Assert.Null(clone.Tags); // A null collection is cloned as null
    }

    [Fact]
    public void WhenNullOwnerThenNullInClone()
    {
        // Arrange
        var original = new DeepCloneDocumentData { Owner = null! };

        // Act
        var clone = original.DeepClone();

        // Assert
        Assert.Null(clone.Owner); // A null reference is cloned as null
    }

    [Fact]
    public void WhenInitAndGetOnlyPropertiesThenAssignableAreCloned()
    {
        // Arrange
        var original = new DeepCloneProfileData { DisplayName = "Alice", Level = 7 };

        // Act
        var clone = original.DeepClone();

        // Assert
        Assert.Equal("Alice", clone.DisplayName); // init is cloned via the object initializer
        Assert.Equal(7, clone.Level);             // set is cloned via assignment
        Assert.Equal("Alice#7", clone.Badge);     // get-only is excluded from cloning but recomputed from the values
    }

    [Fact]
    public void WhenTypeHasIndexerThenIndexerIsExcluded()
    {
        // Arrange
        var original = new DeepCloneIndexedData
        {
            Title = "t",
            ["k"] = "v"
        };

        // Act
        var clone = original.DeepClone();

        // Assert
        Assert.Equal("t", clone.Title); // The indexer is excluded and only Title is cloned
    }
}
