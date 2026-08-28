namespace BunnyTail.CommonCode;

using System.Collections.Generic;

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

    public static IncrementalRunResult RunIncremental<TGenerator>(string source, string addedSource)
        where TGenerator : IIncrementalGenerator, new()
        => Runner<TGenerator>().WithTracking().RunIncremental(source, addedSource);
}
