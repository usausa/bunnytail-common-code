namespace BunnyTail.CommonCode;

using BunnyTail.CommonCode.Generator;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using SourceGenerateHelper.Testing;

using Xunit;

public sealed class PipelineIncrementalityTest
{
    private const string TargetSource = """
        using BunnyTail.CommonCode;

        namespace Demo;

        [GenerateToString]
        public partial class Data
        {
            public int Id { get; set; }

            public string Name { get; set; }
        }

        [GenerateToString]
        public partial class Other
        {
            public long Value { get; set; }
        }
        """;

    private const string UnrelatedSource = "namespace Demo; public static class Runner { public static int Run() => 1; }";

    private static readonly CSharpParseOptions Parse =
        CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);

    private static (GeneratorDriver Driver, Compilation Compilation) CreateDriver() =>
        GeneratorTestRunner
            .For<ToStringGenerator>()
            .WithReference(typeof(GenerateToStringAttribute).Assembly)
            .WithLanguageVersion(LanguageVersion.Preview)
            .WithGlobalOption("build_property.CommonCodeGeneratorToStringSkipLocalsInit", "true")
            .WithTracking()
            .CreateDriver(TargetSource);

    private static SyntaxTree Tree(string source) => CSharpSyntaxTree.ParseText(source, Parse);

#pragma warning disable IDE0028
    private static IncrementalStepRunReason[] OutputReasons(GeneratorDriver driver) =>
        driver.GetRunResult().Results[0].TrackedOutputSteps
            .SelectMany(static x => x.Value)
            .SelectMany(static x => x.Outputs)
            .Select(static x => x.Reason)
            .ToArray();
#pragma warning restore IDE0028

    [Fact]
    public void PipelineKeepsOutputCached()
    {
        // Arrange
        var (driver, compilation) = CreateDriver();
        var target = compilation.SyntaxTrees.First();
        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);
        Assert.NotEmpty(OutputReasons(driver));

        // Act / Assert: adding an unrelated file
        var unrelated = Tree(UnrelatedSource);
        compilation = compilation.AddSyntaxTrees(unrelated);
        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);
        Assert.All(OutputReasons(driver), static x => Assert.Equal(IncrementalStepRunReason.Cached, x));

        // Act / Assert: editing an unrelated file's body
        var unrelatedEdited = Tree(UnrelatedSource.Replace("=> 1;", "=> 1 + 1;", StringComparison.Ordinal));
        compilation = compilation.ReplaceSyntaxTree(unrelated, unrelatedEdited);
        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);
        Assert.All(OutputReasons(driver), static x => Assert.Equal(IncrementalStepRunReason.Cached, x));

        // Act / Assert: re-parsing the target with identical text
        var reparsed = Tree(TargetSource);
        compilation = compilation.ReplaceSyntaxTree(target, reparsed);
        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);
        Assert.All(OutputReasons(driver), static x => Assert.Equal(IncrementalStepRunReason.Cached, x));

        // Act / Assert: a comment at the head shifts every span, which catches a model holding positions
        // A comment at the head shifts every span, which catches a model holding positions.
        var commented = Tree("// header comment\n" + TargetSource);
        compilation = compilation.ReplaceSyntaxTree(reparsed, commented);
        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);
        Assert.All(OutputReasons(driver), static x => Assert.Equal(IncrementalStepRunReason.Cached, x));
    }

    [Fact]
    public void TargetEditRegeneratesOutput()
    {
        // Arrange
        var (driver, compilation) = CreateDriver();
        var target = compilation.SyntaxTrees.First();
        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);

        // Act
        var edited = Tree(TargetSource.Replace(
            "public long Value { get; set; }",
            "public long Value { get; set; }\n\n    public int Extra { get; set; }",
            StringComparison.Ordinal));
        compilation = compilation.ReplaceSyntaxTree(target, edited);
        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);
        var reasons = OutputReasons(driver);

        // Assert
        Assert.NotEmpty(reasons);
        Assert.Contains(reasons, static x => x != IncrementalStepRunReason.Cached);
    }
}
