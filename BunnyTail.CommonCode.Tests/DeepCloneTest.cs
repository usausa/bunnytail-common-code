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
}
