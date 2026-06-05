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

public interface IDelegateToReader
{
    string Read();
}

public interface IDelegateToWriter
{
    void Write(string value);
}

public sealed class DelegateToStorageCore : IDelegateToReader, IDelegateToWriter
{
    private string content = string.Empty;

    public string Read() => content;

    public void Write(string value) => content = value;
}

// 委譲元クラス自身はインターフェースを実装していない (メンバ型が実装するインターフェース経由で解決されることを検証)
[GenerateDelegateTo]
public partial class DelegateToStorageFacade
{
    [DelegateTo]
    private readonly DelegateToStorageCore core = new();
}

// InterfaceType で委譲対象を限定するケース
[GenerateDelegateTo]
public partial class DelegateToReaderFacade
{
    [DelegateTo(InterfaceType = typeof(IDelegateToReader))]
    private readonly DelegateToStorageCore core = new();
}

public interface IDelegateToFormatService
{
    string Format(int value);

    string Format(string value);
}

public sealed class DelegateToFormatCore : IDelegateToFormatService
{
    public string Format(int value) => $"core-int:{value}";

    public string Format(string value) => $"core-string:{value}";
}

// 手書きの Format(int) があっても、別オーバーロード Format(string) は生成で補完される
[GenerateDelegateTo]
public partial class DelegateToFormatFacade : IDelegateToFormatService
{
    [DelegateTo]
    private readonly DelegateToFormatCore core = new();

    public string Format(int value) => $"manual:{value}";
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

    [Fact]
    public void WhenConcreteFieldThenDelegatesAllImplementedInterfaces()
    {
        var facade = new DelegateToStorageFacade();
        facade.Write("payload");
        Assert.Equal("payload", facade.Read());
    }

    [Fact]
    public void WhenInterfaceTypeSpecifiedThenDelegatesOnlyThatInterface()
    {
        var type = typeof(DelegateToReaderFacade);
        Assert.NotNull(type.GetMethod(nameof(IDelegateToReader.Read)));
        Assert.Null(type.GetMethod(nameof(IDelegateToWriter.Write)));
    }

    [Fact]
    public void WhenOverloadHandWrittenThenOtherOverloadIsGenerated()
    {
        // DelegateToFormatFacade : IDelegateToFormatService がコンパイルできる時点で、
        // 手書きの Format(int) に加えて Format(string) が生成されインターフェース実装が完結している
        var facade = new DelegateToFormatFacade();

        Assert.Equal("manual:5", facade.Format(5));        // 手書きの実装
        Assert.Equal("core-string:x", facade.Format("x")); // 生成で補完され core へ委譲
    }
}
