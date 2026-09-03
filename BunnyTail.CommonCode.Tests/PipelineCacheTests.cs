namespace BunnyTail.CommonCode;

using BunnyTail.CommonCode.Generator;

using SourceGenerateHelper.Testing;

public sealed class PipelineCacheTests
{
    private const string UnrelatedSource =
        """
        namespace Other;

        internal sealed class Unrelated;
        """;

    private const string ToStringSource =
        """
        using BunnyTail.CommonCode;

        namespace Test;

        [GenerateToString]
        public partial class Data
        {
            public int Id { get; set; }
        }
        """;

    private const string ToStringAddedSource =
        """
        using BunnyTail.CommonCode;

        namespace Test;

        [GenerateToString]
        public partial class AddedData
        {
            public int Id { get; set; }
        }
        """;

    private const string EqualitySource =
        """
        using BunnyTail.CommonCode;

        namespace Test;

        [GenerateEquality]
        public partial class Data
        {
            public int Id { get; set; }
        }
        """;

    private const string EqualityAddedSource =
        """
        using BunnyTail.CommonCode;

        namespace Test;

        [GenerateEquality]
        public partial class AddedData
        {
            public int Id { get; set; }
        }
        """;

    private const string CompareToSource =
        """
        using BunnyTail.CommonCode;

        namespace Test;

        [GenerateCompareTo]
        public partial class Data
        {
            [CompareKey]
            public int Id { get; set; }
        }
        """;

    private const string CompareToAddedSource =
        """
        using BunnyTail.CommonCode;

        namespace Test;

        [GenerateCompareTo]
        public partial class AddedData
        {
            [CompareKey]
            public int Id { get; set; }
        }
        """;

    private const string DeepCloneSource =
        """
        using BunnyTail.CommonCode;

        namespace Test;

        [GenerateDeepClone]
        public partial class Data : IDeepCloneable<Data>
        {
            public string Name { get; set; } = default!;
        }
        """;

    private const string DeepCloneAddedSource =
        """
        using BunnyTail.CommonCode;

        namespace Test;

        [GenerateDeepClone]
        public partial class AddedData : IDeepCloneable<AddedData>
        {
            public string Name { get; set; } = default!;
        }
        """;

    private const string DelegateToSource =
        """
        using BunnyTail.CommonCode;

        namespace Test;

        public interface IService
        {
            string GetMessage();
        }

        public sealed class ServiceCore : IService
        {
            public string GetMessage() => "core";
        }

        [GenerateDelegateTo]
        public partial class Service : IService
        {
            [DelegateTo]
            private readonly ServiceCore inner = new();
        }
        """;

    private const string DelegateToAddedSource =
        """
        using BunnyTail.CommonCode;

        namespace Test;

        [GenerateDelegateTo]
        public partial class AddedService : IService
        {
            [DelegateTo]
            private readonly ServiceCore inner = new();
        }
        """;

    // ------------------------------------------------------------
    // ToString
    // ------------------------------------------------------------

    [Fact]
    public void UnrelatedEditKeepsToStringModelCached()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunIncremental<ToStringGenerator>(ToStringSource, UnrelatedSource);

        // Assert
        Assert.Equal(result.FirstGeneratedText, result.SecondGeneratedText);
        Assert.NotEmpty(result.OutputReasons);
        Assert.DoesNotContain(result.OutputReasons, static x => x.IsChanged());
    }

    [Fact]
    public void ToStringEditRebuildsModel()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunIncremental<ToStringGenerator>(ToStringSource, ToStringAddedSource);

        // Assert
        Assert.Contains(result.OutputReasons, static x => x.IsChanged());
    }

    // ------------------------------------------------------------
    // Equality
    // ------------------------------------------------------------

    [Fact]
    public void UnrelatedEditKeepsEqualityModelCached()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunIncremental<EqualityGenerator>(EqualitySource, UnrelatedSource);

        // Assert
        Assert.Equal(result.FirstGeneratedText, result.SecondGeneratedText);
        Assert.NotEmpty(result.OutputReasons);
        Assert.DoesNotContain(result.OutputReasons, static x => x.IsChanged());
    }

    [Fact]
    public void EqualityEditRebuildsModel()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunIncremental<EqualityGenerator>(EqualitySource, EqualityAddedSource);

        // Assert
        Assert.Contains(result.OutputReasons, static x => x.IsChanged());
    }

    // ------------------------------------------------------------
    // CompareTo
    // ------------------------------------------------------------

    [Fact]
    public void UnrelatedEditKeepsCompareToModelCached()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunIncremental<CompareToGenerator>(CompareToSource, UnrelatedSource);

        // Assert
        Assert.Equal(result.FirstGeneratedText, result.SecondGeneratedText);
        Assert.NotEmpty(result.OutputReasons);
        Assert.DoesNotContain(result.OutputReasons, static x => x.IsChanged());
    }

    [Fact]
    public void CompareToEditRebuildsModel()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunIncremental<CompareToGenerator>(CompareToSource, CompareToAddedSource);

        // Assert
        Assert.Contains(result.OutputReasons, static x => x.IsChanged());
    }

    // ------------------------------------------------------------
    // DeepClone
    // ------------------------------------------------------------

    [Fact]
    public void UnrelatedEditKeepsDeepCloneModelCached()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunIncremental<DeepCloneGenerator>(DeepCloneSource, UnrelatedSource);

        // Assert
        Assert.Equal(result.FirstGeneratedText, result.SecondGeneratedText);
        Assert.NotEmpty(result.OutputReasons);
        Assert.DoesNotContain(result.OutputReasons, static x => x.IsChanged());
    }

    [Fact]
    public void DeepCloneEditRebuildsModel()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunIncremental<DeepCloneGenerator>(DeepCloneSource, DeepCloneAddedSource);

        // Assert
        Assert.Contains(result.OutputReasons, static x => x.IsChanged());
    }

    // ------------------------------------------------------------
    // DelegateTo
    // ------------------------------------------------------------

    [Fact]
    public void UnrelatedEditKeepsDelegateToModelCached()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunIncremental<DelegateToGenerator>(DelegateToSource, UnrelatedSource);

        // Assert
        Assert.Equal(result.FirstGeneratedText, result.SecondGeneratedText);
        Assert.NotEmpty(result.OutputReasons);
        Assert.DoesNotContain(result.OutputReasons, static x => x.IsChanged());
    }

    [Fact]
    public void DelegateToEditRebuildsModel()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunIncremental<DelegateToGenerator>(DelegateToSource, DelegateToAddedSource);

        // Assert
        Assert.Contains(result.OutputReasons, static x => x.IsChanged());
    }
}
