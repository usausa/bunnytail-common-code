namespace BunnyTail.CommonCode;

using System.Collections.Generic;

using BunnyTail.CommonCode.Generator;

using Microsoft.CodeAnalysis;

using SourceGenerateHelper.Testing;

// Driver-based harness for diagnostic scenarios.
// The runtime-behaviour tests cover generated code that works; these cover the refusals,
// which by construction the runtime tests cannot reach.
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
}
