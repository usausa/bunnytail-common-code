namespace BunnyTail.CommonCode;

public interface IDelegateToSimpleService
{
    string GetMessage();
    void Reset();
    int Count { get; set; }
}

[GenerateDelegateTo]
public partial class DelegateToLoggingService : IDelegateToSimpleService
{
    [DelegateTo]
    private readonly DelegateToSimpleServiceCore inner = new();
}

public sealed class DelegateToSimpleServiceCore : IDelegateToSimpleService
{
    public int Count { get; set; }

    public string GetMessage() => $"Hello-{Count}";

    public void Reset() => Count = 0;
}

public class DelegateToTest
{
    [Fact]
    public void WhenGetMessageCalledThenDelegateToInner()
    {
        var svc = new DelegateToLoggingService();
        Assert.Equal("Hello-0", svc.GetMessage());
    }

    [Fact]
    public void WhenCountSetThenDelegatesToInner()
    {
        var svc = new DelegateToLoggingService { Count = 5 };
        Assert.Equal("Hello-5", svc.GetMessage());
    }

    [Fact]
    public void WhenResetCalledThenCountReturnsZero()
    {
        var svc = new DelegateToLoggingService { Count = 10 };
        svc.Reset();
        Assert.Equal(0, svc.Count);
    }

    [Fact]
    public void WhenUsedAsInterfaceThenWorksCorrectly()
    {
        var svc = new DelegateToLoggingService { Count = 3 };
        Assert.Equal("Hello-3", svc.GetMessage());
        svc.Reset();
        Assert.Equal(0, svc.Count);
    }
}
