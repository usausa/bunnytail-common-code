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
    // init 専用: オブジェクト初期化子で複製される
    public string DisplayName { get; init; } = default!;

    // set 可能: 代入で複製される
    public int Level { get; set; }

    // get-only (計算プロパティ): 代入不能なので複製対象外
    public string Badge => $"{DisplayName}#{Level}";
}

// インデクサは複製対象外 (clone.<Name> で代入できないため)
[GenerateDeepClone]
public partial class DeepCloneIndexedData : IDeepCloneable<DeepCloneIndexedData>
{
    private readonly Dictionary<string, string> map = [];

    public string Title { get; set; } = default!;

    public string this[string key]
    {
        get => this.map[key];
        set => this.map[key] = value;
    }
}

public class DeepCloneTest
{
    [Fact]
    public void WhenClonedThenIndependentCopy()
    {
        var original = new DeepCloneDocumentData
        {
            Title = "Hello",
            Tags = ["a", "b"],
            Scores = [1, 2, 3],
            Owner = new DeepCloneAuthorData { Name = "Alice" },
            ExtraRef = new object(),
            CacheKey = 42
        };

        var clone = original.DeepClone();

        Assert.Equal(original.Title, clone.Title);
        Assert.Equal(original.Tags, clone.Tags);
        Assert.Equal(original.Scores, clone.Scores);
        Assert.Equal(original.Owner.Name, clone.Owner.Name);
    }

    [Fact]
    public void WhenTagsModifiedThenOriginalUnchanged()
    {
        var original = new DeepCloneDocumentData { Tags = ["a", "b"] };
        var clone = original.DeepClone();

        clone.Tags.Add("c");

        Assert.Equal(2, original.Tags.Count);
        Assert.Equal(3, clone.Tags.Count);
    }

    [Fact]
    public void WhenScoresModifiedThenOriginalUnchanged()
    {
        var original = new DeepCloneDocumentData { Scores = [1, 2] };
        var clone = original.DeepClone();

        clone.Scores[0] = 99;

        Assert.Equal(1, original.Scores[0]);
    }

    [Fact]
    public void WhenOwnerModifiedThenOriginalUnchanged()
    {
        var original = new DeepCloneDocumentData { Owner = new DeepCloneAuthorData { Name = "Alice" } };
        var clone = original.DeepClone();

        clone.Owner.Name = "Bob";

        Assert.Equal("Alice", original.Owner.Name);
    }

    [Fact]
    public void WhenShallowClonePropModifiedThenBothSeeChange()
    {
        var sharedRef = new object();
        var original = new DeepCloneDocumentData { ExtraRef = sharedRef };
        var clone = original.DeepClone();

        Assert.Same(sharedRef, clone.ExtraRef);
    }

    [Fact]
    public void WhenCloneIgnorePropThenNotCopied()
    {
        var original = new DeepCloneDocumentData { CacheKey = 42 };
        var clone = original.DeepClone();

        Assert.Equal(0, clone.CacheKey);
    }

    [Fact]
    public void WhenNullTagsThenNullInClone()
    {
        var original = new DeepCloneDocumentData { Tags = null! };
        var clone = original.DeepClone();

        Assert.Null(clone.Tags);
    }

    [Fact]
    public void WhenNullOwnerThenNullInClone()
    {
        var original = new DeepCloneDocumentData { Owner = null! };
        var clone = original.DeepClone();

        Assert.Null(clone.Owner);
    }

    [Fact]
    public void WhenInitAndGetOnlyPropertiesThenAssignableAreCloned()
    {
        var original = new DeepCloneProfileData { DisplayName = "Alice", Level = 7 };

        var clone = original.DeepClone();

        Assert.Equal("Alice", clone.DisplayName); // init はオブジェクト初期化子で複製
        Assert.Equal(7, clone.Level);             // set は代入で複製
        Assert.Equal("Alice#7", clone.Badge);     // get-only は複製対象外だが値から再計算される
    }

    [Fact]
    public void WhenTypeHasIndexerThenIndexerIsExcluded()
    {
        var original = new DeepCloneIndexedData { Title = "t" };
        original["k"] = "v";

        var clone = original.DeepClone();

        Assert.Equal("t", clone.Title); // インデクサは対象外で Title のみ複製
    }
}
