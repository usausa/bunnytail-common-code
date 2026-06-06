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

public interface IDelegateToManualMethod
{
    string GetMessage();

    void Reset();
}

public sealed class DelegateToManualMethodCore : IDelegateToManualMethod
{
    public int ResetCount { get; private set; }

    public string GetMessage() => "core-message";

    public void Reset() => ResetCount++;
}

// オーバーロードの無い単一メソッド GetMessage() を手書きした場合、その委譲は生成されず、
// 手書きしていない Reset() のみ生成されることを検証する
[GenerateDelegateTo]
public partial class DelegateToManualMethodFacade : IDelegateToManualMethod
{
    [DelegateTo]
    private readonly DelegateToManualMethodCore core = new();

    public string GetMessage() => "manual-message";

    public int CoreResetCount => core.ResetCount;
}

public interface IDelegateToManualProperty
{
    int Value { get; set; }

    string Describe();
}

public sealed class DelegateToManualPropertyCore : IDelegateToManualProperty
{
    public int Value { get; set; }

    public string Describe() => $"core:{Value}";
}

// Value プロパティを手書きした場合、その委譲は生成されず、手書きしていない Describe() のみ生成されることを検証する
[GenerateDelegateTo]
public partial class DelegateToManualPropertyFacade : IDelegateToManualProperty
{
    [DelegateTo]
    private readonly DelegateToManualPropertyCore core = new();

    // 手書きの Value。委譲が生成された場合は core.Value へ転送されるが、手書きのため core とは独立している
    public int Value { get; set; }
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

    [Fact]
    public void WhenMethodHandWrittenThenSkippedAndSiblingGenerated()
    {
        var facade = new DelegateToManualMethodFacade();

        // 手書きの GetMessage() がそのまま使われる (委譲版は生成されない)
        Assert.Equal("manual-message", facade.GetMessage());

        // 生成による重複定義が無く、GetMessage の宣言は手書きの 1 つだけ
        Assert.Single(typeof(DelegateToManualMethodFacade).GetMember(nameof(IDelegateToManualMethod.GetMessage)));

        // 手書きしていない Reset() は生成され core へ委譲される
        facade.Reset();
        facade.Reset();
        Assert.Equal(2, facade.CoreResetCount);
    }

    [Fact]
    public void WhenPropertyHandWrittenThenSkippedAndMethodGenerated()
    {
        var facade = new DelegateToManualPropertyFacade { Value = 10 };

        // 手書きの Value がそのまま保持される
        Assert.Equal(10, facade.Value);

        // 生成による重複定義が無く、Value の宣言は手書きの 1 つだけ
        Assert.Single(typeof(DelegateToManualPropertyFacade).GetMember(nameof(IDelegateToManualProperty.Value)));

        // 委譲が生成されていれば core.Value も 10 になるが、手書きスキップのため core は 0 のまま
        Assert.Equal("core:0", facade.Describe());
    }
}
