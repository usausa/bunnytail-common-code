namespace BunnyTail.CommonCode.Generator;

using System;
using System.Collections.Generic;

using Microsoft.CodeAnalysis;

using SourceGenerateHelper;

internal static class MemberCollector
{
    public static List<ISymbol> GetInstanceMembers(INamedTypeSymbol type)
    {
        var levels = new List<List<ISymbol>>();
        var hidden = new HashSet<string>(StringComparer.Ordinal);
        for (var current = type; current is not null; current = current.BaseType)
        {
            var isBase = !SymbolEqualityComparer.Default.Equals(current, type);
            var level = new List<ISymbol>();
            var declared = new List<string>();
            foreach (var member in current.GetMembers())
            {
                if (isBase && (member.DeclaredAccessibility == Accessibility.Private))
                {
                    continue;
                }

                declared.Add(member.Name);
                if (!member.IsStatic &&
                    !hidden.Contains(member.Name) &&
                    (member is IPropertySymbol { IsIndexer: false } or IFieldSymbol { AssociatedSymbol: null, IsImplicitlyDeclared: false }) &&
                    !(member.IsObsolete(out var isError) && isError))
                {
                    level.Add(member);
                }
            }

            hidden.UnionWith(declared);
            levels.Add(level);
        }

        var members = new List<ISymbol>();
        for (var i = levels.Count - 1; i >= 0; i--)
        {
            members.AddRange(levels[i]);
        }

        return members;
    }
}
