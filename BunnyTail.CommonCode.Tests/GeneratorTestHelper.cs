namespace BunnyTail.CommonCode;

using System.Collections.Generic;
using System.Runtime.Loader;

using BunnyTail.CommonCode.Generator;

using Microsoft.CodeAnalysis;

using SourceGenerateHelper.Testing;

internal static class GeneratorTestHelper
{
    private static GeneratorTestRunner Runner<TGenerator>()
        where TGenerator : IIncrementalGenerator, new()
        => GeneratorTestRunner
            .For<TGenerator>()
            .WithReference(typeof(GenerateEqualityAttribute).Assembly)
            .WithDiagnosticPrefix("BTCC");

    public static IReadOnlyList<Diagnostic> GetDiagnostics<TGenerator>(string source)
        where TGenerator : IIncrementalGenerator, new()
        => Runner<TGenerator>().GetDiagnostics(source);

    public static IReadOnlyList<Diagnostic> GetDiagnosticsAll<TGenerator>(string source)
        where TGenerator : IIncrementalGenerator, new()
        => Runner<TGenerator>().GetDiagnosticsAll(source);

    public static string GetGeneratedSource<TGenerator>(string source)
        where TGenerator : IIncrementalGenerator, new()
        => Runner<TGenerator>().GetGeneratedSource(source);

    public static GeneratorTestResult Run<TGenerator>(string source)
        where TGenerator : IIncrementalGenerator, new()
        => Runner<TGenerator>().Run(source);

    public static GeneratorTestResult RunWithOption<TGenerator>(string name, string value, string source)
        where TGenerator : IIncrementalGenerator, new()
        => Runner<TGenerator>().WithGlobalOption($"build_property.{name}", value).Run(source);

    private static GeneratorTestRunner AllRunner() =>
        new GeneratorTestRunner(
                new ToStringGenerator(),
                new EqualityGenerator(),
                new DeepCloneGenerator(),
                new DelegateToGenerator(),
                new CompareToGenerator())
            .WithReference(typeof(GenerateEqualityAttribute).Assembly);

    public static IReadOnlyList<string> GetProblemIds(string source) =>
        [.. AllRunner().GetProblems(source).Select(static x => x.Id)];

    public static string GetAllGeneratedSource(string source) =>
        AllRunner().Run(source).AllGeneratedText;

    // Compiles the source with all generators and returns the result of Test.Program.Run()
    public static string Execute(string source)
    {
        var result = AllRunner().Run(source);
        Assert.True(result.Problems.Count == 0, String.Join(Environment.NewLine, result.Problems));

        using var stream = new MemoryStream();
        var emitResult = result.OutputCompilation.Emit(stream);
        Assert.True(emitResult.Success, String.Join(Environment.NewLine, emitResult.Diagnostics));

        stream.Position = 0;
        var assembly = new AssemblyLoadContext(null, isCollectible: true).LoadFromStream(stream);
        return (string)assembly.GetType("Test.Program", throwOnError: true)!.GetMethod("Run")!.Invoke(null, null)!;
    }

    public static IncrementalRunResult RunIncremental<TGenerator>(string source, string addedSource)
        where TGenerator : IIncrementalGenerator, new()
        => Runner<TGenerator>().WithTracking().RunIncremental(source, addedSource);
}
