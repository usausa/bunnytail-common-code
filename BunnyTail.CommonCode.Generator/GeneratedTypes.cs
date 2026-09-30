namespace BunnyTail.CommonCode.Generator;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using SourceGenerateHelper;

internal interface IGeneratedType
{
    string HintName { get; }

    string DisplayName { get; }
}

internal static class GeneratedTypes
{
    public static bool IsExtendable(TypeDeclarationSyntax syntax, INamedTypeSymbol symbol)
    {
        for (SyntaxNode? node = syntax; node is TypeDeclarationSyntax declaration; node = declaration.Parent)
        {
            if (!declaration.Modifiers.Any(SyntaxKind.PartialKeyword))
            {
                return false;
            }
        }

        for (var type = symbol; type is not null; type = type.ContainingType)
        {
            if (type.IsFileLocal)
            {
                return false;
            }
        }

        return true;
    }

    public static IEnumerable<DiagnosticInfo> SelectDiagnostics<T>(ImmutableArray<Result<T>> results, string attributeName)
        where T : IEquatable<T>, IGeneratedType =>
        results.SelectError()
            .Concat(FindHintNameCollisions(results).Select(x => new DiagnosticInfo(Diagnostics.HintNameCollision, (Location?)null, attributeName, x.Name, x.Other)));

    public static ImmutableArray<T> SelectTypes<T>(ImmutableArray<Result<T>> results)
        where T : IEquatable<T>, IGeneratedType
    {
        var collisions = new HashSet<string>(FindHintNameCollisions(results).Select(static x => x.HintName), StringComparer.Ordinal);
        var generated = new HashSet<string>(StringComparer.Ordinal);
        return results.SelectValue()
            .Where(x => !collisions.Contains(x.HintName) && generated.Add(x.HintName))
            .ToImmutableArray();
    }

    private static List<(string HintName, string Name, string Other)> FindHintNameCollisions<T>(ImmutableArray<Result<T>> results)
        where T : IEquatable<T>, IGeneratedType
    {
        var collisions = new List<(string HintName, string Name, string Other)>();
        var firsts = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        var reported = new HashSet<string>(StringComparer.Ordinal);
        foreach (var type in results.SelectValue().OrderBy(static x => x.HintName, StringComparer.Ordinal))
        {
            if (!firsts.TryGetValue(type.HintName, out var first))
            {
                firsts.Add(type.HintName, type);
            }
            else if ((first.HintName != type.HintName) && reported.Add(type.HintName))
            {
                collisions.Add((type.HintName, type.DisplayName, first.DisplayName));
            }
        }

        return collisions;
    }
}
