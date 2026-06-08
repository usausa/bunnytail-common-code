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

// The delegating class itself does not implement the interface (verifies resolution via interfaces implemented by the member type)
[GenerateDelegateTo]
public partial class DelegateToStorageFacade
{
    [DelegateTo]
    private readonly DelegateToStorageCore core = new();
}

// Case where the delegation target is restricted by InterfaceType
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

// Even with a hand-written Format(int), the other overload Format(string) is filled in by generation
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

// When a single non-overloaded method GetMessage() is hand-written, its delegation is not generated,
// and only the non-hand-written Reset() is generated
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

// When the Value property is hand-written, its delegation is not generated, and only the non-hand-written Describe() is generated
[GenerateDelegateTo]
public partial class DelegateToManualPropertyFacade : IDelegateToManualProperty
{
    [DelegateTo]
    private readonly DelegateToManualPropertyCore core = new();

    // Hand-written Value. If delegation were generated it would forward to core.Value, but being hand-written it is independent of core
    public int Value { get; set; }
}

public class DelegateToTest
{
    [Fact]
    public void WhenGetMessageCalledThenDelegateToInner()
    {
        // Arrange
        var svc = new DelegateToLoggingService();

        // Act
        var result = svc.GetMessage();

        // Assert
        Assert.Equal("Hello-0", result); // Delegated to the inner member
    }

    [Fact]
    public void WhenCountSetThenDelegatesToInner()
    {
        // Arrange
        var svc = new DelegateToLoggingService { Count = 5 };

        // Act
        var result = svc.GetMessage();

        // Assert
        Assert.Equal("Hello-5", result); // Property setting is also delegated to the inner member
    }

    [Fact]
    public void WhenResetCalledThenCountReturnsZero()
    {
        // Arrange
        var svc = new DelegateToLoggingService { Count = 10 };

        // Act
        svc.Reset();

        // Assert
        Assert.Equal(0, svc.Count); // Method calls are also delegated
    }

    [Fact]
    public void WhenUsedAsInterfaceThenWorksCorrectly()
    {
        // Arrange
        var svc = new DelegateToLoggingService { Count = 3 };

        // Act
        var message = svc.GetMessage();
        svc.Reset();

        // Assert
        Assert.Equal("Hello-3", message);
        Assert.Equal(0, svc.Count);
    }

    [Fact]
    public void WhenConcreteFieldThenDelegatesAllImplementedInterfaces()
    {
        // Arrange
        var facade = new DelegateToStorageFacade();

        // Act
        facade.Write("payload");

        // Assert
        Assert.Equal("payload", facade.Read()); // Delegated to all interfaces implemented by the member type
    }

    [Fact]
    public void WhenInterfaceTypeSpecifiedThenDelegatesOnlyThatInterface()
    {
        // Arrange
        var type = typeof(DelegateToReaderFacade);

        // Act
        var readMethod = type.GetMethod(nameof(IDelegateToReader.Read));
        var writeMethod = type.GetMethod(nameof(IDelegateToWriter.Write));

        // Assert
        Assert.NotNull(readMethod); // Only IDelegateToReader specified by InterfaceType is generated
        Assert.Null(writeMethod);   // The unspecified IDelegateToWriter is not generated
    }

    [Fact]
    public void WhenOverloadHandWrittenThenOtherOverloadIsGenerated()
    {
        // Arrange
        // The fact that DelegateToFormatFacade : IDelegateToFormatService compiles means that,
        // in addition to the hand-written Format(int), Format(string) is generated and the interface implementation is complete
        var facade = new DelegateToFormatFacade();

        // Act
        var manual = facade.Format(5);
        var generated = facade.Format("x");

        // Assert
        Assert.Equal("manual:5", manual);          // Hand-written implementation
        Assert.Equal("core-string:x", generated);  // Filled in by generation and delegated to core
    }

    [Fact]
    public void WhenMethodHandWrittenThenSkippedAndSiblingGenerated()
    {
        // Arrange
        var facade = new DelegateToManualMethodFacade();

        // Act
        facade.Reset();
        facade.Reset();

        // Assert
        // The hand-written GetMessage() is used as is (the delegating version is not generated)
        Assert.Equal("manual-message", facade.GetMessage());

        // No duplicate definition from generation; there is only the single hand-written GetMessage declaration
        Assert.Single(typeof(DelegateToManualMethodFacade).GetMember(nameof(IDelegateToManualMethod.GetMessage)));

        // The non-hand-written Reset() is generated and delegated to core
        Assert.Equal(2, facade.CoreResetCount);
    }

    [Fact]
    public void WhenPropertyHandWrittenThenSkippedAndMethodGenerated()
    {
        // Arrange
        var facade = new DelegateToManualPropertyFacade { Value = 10 };

        // Act
        var describe = facade.Describe();

        // Assert
        // The hand-written Value is retained as is
        Assert.Equal(10, facade.Value);

        // No duplicate definition from generation; there is only the single hand-written Value declaration
        Assert.Single(typeof(DelegateToManualPropertyFacade).GetMember(nameof(IDelegateToManualProperty.Value)));

        // If delegation were generated core.Value would also be 10, but because it is skipped for hand-written members core stays 0
        Assert.Equal("core:0", describe);
    }
}
