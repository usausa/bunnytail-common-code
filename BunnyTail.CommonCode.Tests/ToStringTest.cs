namespace BunnyTail.CommonCode;

public class ToStringTest
{
    [Fact]
    public void TestBasic()
    {
        Assert.Equal(
            "{ Id = 123, Name = xyz, IntValues = [1, 2], StringValues = [a, null] }",
            new Data { Id = 123, Name = "xyz", IntValues = [1, 2], StringValues = ["a", null] }.ToString());
        Assert.Equal(
            "{ Id = 123, Name = xyz, IntValues = null, StringValues = null }",
            new Data { Id = 123, Name = "xyz" }.ToString());
    }

    [Fact]
    public void TestGeneric()
    {
        Assert.Equal(
            "{ Value = 123 }",
            new GenericData<int> { Value = 123 }.ToString());
        Assert.Equal(
            "{ Value = xyz }",
            new GenericData<string> { Value = "xyz" }.ToString());
        Assert.Equal(
            "{ Value = null }",
            new GenericData<string?> { Value = null }.ToString());
    }

    [Fact]
    public void TestInnerClass()
    {
        Assert.Equal(
            "{ Id = 456, Name = inner }",
            new OuterData.InnerData { Id = 456, Name = "inner" }.ToString());
    }
}
