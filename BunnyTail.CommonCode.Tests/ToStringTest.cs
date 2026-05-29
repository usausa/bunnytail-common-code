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
}
