namespace BunnyTail.CommonCode;

#pragma warning disable CA1819
// ReSharper disable once PartialTypeWithSinglePart
[GenerateToString]
public partial class ToStringData
{
    public int Id { get; set; }

    public string Name { get; set; } = default!;

    public int[] IntValues { get; set; } = default!;

    public string?[] StringValues { get; set; } = default!;

    [IgnoreToString]
    public int Ignore { get; set; }
}
#pragma warning restore CA1819

// ReSharper disable once PartialTypeWithSinglePart
[GenerateToString]
public partial class ToStringGenericData<T>
{
    public T Value { get; set; } = default!;
}

public static partial class ToStringOuterData
{
    [GenerateToString]
    public partial class InnerData
    {
        public int Id { get; set; }

        public string Name { get; set; } = default!;
    }
}

public class ToStringShadowBase
{
    public int Value { get; init; }
}

// 派生型が基底プロパティを new で隠蔽しても、最派生の宣言のみ出力する (二重出力しない)
[GenerateToString]
public partial class ToStringShadowDerived : ToStringShadowBase
{
    public new string Value { get; init; } = default!;
}

// インデクサは出力対象外
[GenerateToString]
public partial class ToStringIndexedData
{
    private readonly Dictionary<string, string> map = [];

    public string Name { get; init; } = default!;

    public string this[string key]
    {
        get => this.map[key];
        set => this.map[key] = value;
    }
}

public class ToStringHiddenBase
{
    public string Token { get; init; } = default!;
}

// 派生が public new を IgnoreToString で隠蔽。this.Token は派生に束縛され基底 public に到達できないため出力対象外
[GenerateToString]
public partial class ToStringHiddenDerived : ToStringHiddenBase
{
    [IgnoreToString]
    public new int Token { get; init; }

    public string Label { get; init; } = default!;
}

public class ToStringTest
{
    [Fact]
    public void TestBasic()
    {
        Assert.Equal(
            "{ Id = 123, Name = xyz, IntValues = [1, 2], StringValues = [a, null] }",
            new ToStringData { Id = 123, Name = "xyz", IntValues = [1, 2], StringValues = ["a", null] }.ToString());
        Assert.Equal(
            "{ Id = 123, Name = xyz, IntValues = null, StringValues = null }",
            new ToStringData { Id = 123, Name = "xyz" }.ToString());
    }

    [Fact]
    public void TestGeneric()
    {
        Assert.Equal(
            "{ Value = 123 }",
            new ToStringGenericData<int> { Value = 123 }.ToString());
        Assert.Equal(
            "{ Value = xyz }",
            new ToStringGenericData<string> { Value = "xyz" }.ToString());
        Assert.Equal(
            "{ Value = null }",
            new ToStringGenericData<string?> { Value = null }.ToString());
    }

    [Fact]
    public void TestInnerClass()
    {
        Assert.Equal(
            "{ Id = 456, Name = inner }",
            new ToStringOuterData.InnerData { Id = 456, Name = "inner" }.ToString());
    }

    [Fact]
    public void TestShadowedProperty()
    {
        // 隠蔽された基底 int Value は出力されず、最派生 string Value のみ (二重出力しない)
        Assert.Equal(
            "{ Value = x }",
            new ToStringShadowDerived { Value = "x" }.ToString());
    }

    [Fact]
    public void TestIndexerExcluded()
    {
        var data = new ToStringIndexedData { Name = "x" };
        data["k"] = "v";

        Assert.Equal("{ Name = x }", data.ToString());
    }

    [Fact]
    public void TestHiddenPropertyExcluded()
    {
        // 隠蔽 + IgnoreToString の Token は出力されず、Label のみ
        Assert.Equal(
            "{ Label = L }",
            new ToStringHiddenDerived { Token = 1, Label = "L" }.ToString());
    }
}
