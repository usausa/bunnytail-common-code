namespace BunnyTail.CommonCode.Generator;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

using SourceGenerateHelper;

[Generator]
public sealed class EqualityGenerator : IIncrementalGenerator
{
    private const string GenerateAttributeName = "BunnyTail.CommonCode.GenerateEqualityAttribute";
    private const string IgnoreAttributeName = "BunnyTail.CommonCode.IgnoreEqualityAttribute";

    // ------------------------------------------------------------
    // Initialize
    // ------------------------------------------------------------

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var targetProvider = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                GenerateAttributeName,
                static (node, _) => node is ClassDeclarationSyntax or StructDeclarationSyntax,
                static (ctx, _) => GetTypeModel(ctx))
            .SelectMany(static (x, _) => x is not null ? ImmutableArray.Create(x) : []);

        context.RegisterSourceOutput(
            targetProvider,
            static (spc, result) => ReportDiagnostics(spc, result));

        // Generation flows from the per-type provider instead of a Collect()ed array, so
        // editing one type invalidates only that type's output, not every type's.
        var models = targetProvider
            .Where(static x => x.HasValue)
            .Select(static (x, _) => x.Value)
            .WithTrackingName("Models");
        context.RegisterImplementationSourceOutput(
            models,
            static (spc, type) => Execute(spc, type));
    }

    private static Result<TypeModel> GetTypeModel(GeneratorAttributeSyntaxContext context)
    {
        var syntax = (TypeDeclarationSyntax)context.TargetNode;
        var symbol = (INamedTypeSymbol)context.TargetSymbol;

        if (!syntax.Modifiers.Any(static x => x.IsKind(SyntaxKind.PartialKeyword)))
        {
            return Results.Error<TypeModel>(new DiagnosticInfo(Diagnostics.EqualityInvalidTypeDefinition, syntax.Identifier.GetLocation(), symbol.Name));
        }

        var ns = String.IsNullOrEmpty(symbol.ContainingNamespace.Name) ? string.Empty : symbol.ContainingNamespace.ToDisplayString();

        var containingTypes = default(List<ContainingTypeModel>?);
        var containingSymbol = symbol.ContainingType;
        while (containingSymbol is not null)
        {
            containingTypes ??= [];
            containingTypes.Add(new ContainingTypeModel(containingSymbol.GetClassName(), containingSymbol.IsValueType));
            containingSymbol = containingSymbol.ContainingType;
        }
        containingTypes?.Reverse();

        var attr = symbol.GetAttributes().First(static x => x.AttributeClass?.ToDisplayString() == GenerateAttributeName);

        var generateOperators = GetBoolArg(attr, nameof(TypeModel.GenerateOperators)) ?? true;
        var deepCollectionEquality = GetBoolArg(attr, nameof(TypeModel.DeepCollectionEquality)) ?? false;

        // For equality / hash, collect reachable public properties walking up to base types (flat spec)
        var properties = new List<PropertyModel>();
        var seenNames = new HashSet<string>(StringComparer.Ordinal);
        var currentSymbol = symbol;
        while (currentSymbol is not null)
        {
            foreach (var member in currentSymbol.GetMembers().OfType<IPropertySymbol>())
            {
                // Properties can not be overloaded, so a name seen at a more-derived level is always
                // the reachable one (override or `new` hide); the base declaration is skipped. This is
                // unlike methods, where the same name can carry distinct signatures.
                if (!seenNames.Add(member.Name))
                {
                    continue;
                }

                // Exclude indexers and non-public properties
                if (member.IsIndexer || (member.DeclaredAccessibility != Accessibility.Public))
                {
                    continue;
                }

                if ((member.GetMethod is null) || member.IsWriteOnly)
                {
                    continue;
                }

                if (member.GetAttributes().Any(static x => x.AttributeClass?.ToDisplayString() == IgnoreAttributeName))
                {
                    continue;
                }

                properties.Add(new PropertyModel(
                    member.Name,
                    ClassifyCollection(member.Type),
                    member.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
            }
            currentSymbol = currentSymbol.BaseType;
        }

        if (properties.Count == 0)
        {
            return Results.Error<TypeModel>(new DiagnosticInfo(Diagnostics.EqualityNoProperties, syntax.Identifier.GetLocation(), symbol.Name));
        }

        return Results.Success(new TypeModel(
            ns,
            new EquatableArray<ContainingTypeModel>(containingTypes?.ToArray() ?? []),
            symbol.GetClassName(),
            symbol.IsValueType,
            symbol.IsSealed,
            generateOperators,
            deepCollectionEquality,
            new EquatableArray<PropertyModel>(properties.ToArray())));
    }

    private static bool? GetBoolArg(AttributeData attr, string name)
    {
        var arg = attr.NamedArguments.FirstOrDefault(x => x.Key == name);
        if (arg.Value.IsNull)
        {
            return null;
        }

        if (arg.Value.Value is bool b)
        {
            return b;
        }

        return null;
    }

    // Set / Dictionary enumerate in insertion / rehash order, so an ordered SequenceEqual would
    // report two logically equal instances as different. They are classified as Unordered and
    // compared without regard to order; arrays and lists stay Sequence.
    private static CollectionKind ClassifyCollection(ITypeSymbol typeSymbol)
    {
        if (typeSymbol.SpecialType == SpecialType.System_String)
        {
            return CollectionKind.None;
        }

        if (typeSymbol is IArrayTypeSymbol)
        {
            return CollectionKind.Sequence;
        }

        var isEnumerable = false;
        foreach (var type in Self(typeSymbol).Concat(typeSymbol.AllInterfaces))
        {
            if (type is not INamedTypeSymbol { IsGenericType: true } named)
            {
                continue;
            }

            switch (named.ConstructedFrom.ToDisplayString())
            {
                case "System.Collections.Generic.ISet<T>":
                case "System.Collections.Generic.IReadOnlySet<T>":
                case "System.Collections.Generic.IDictionary<TKey, TValue>":
                case "System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>":
                    return CollectionKind.Unordered;
                case "System.Collections.Generic.IEnumerable<T>":
                    isEnumerable = true;
                    break;
            }
        }

        return isEnumerable ? CollectionKind.Sequence : CollectionKind.None;

        static IEnumerable<ITypeSymbol> Self(ITypeSymbol symbol)
        {
            yield return symbol;
        }
    }

    // ------------------------------------------------------------
    // Generator
    // ------------------------------------------------------------

    private static void ReportDiagnostics(SourceProductionContext context, Result<TypeModel> result)
    {
        foreach (var info in result.Diagnostics)
        {
            context.ReportDiagnostic(info);
        }
    }

    private static void Execute(SourceProductionContext context, TypeModel type)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        var builder = new SourceBuilder();
        BuildSource(builder, type);

        var filename = MakeFilename(type.Namespace, type.ContainingTypes, type.ClassName, "Equality");
        context.AddSource(filename, SourceText.From(builder.ToString(), Encoding.UTF8));
    }

    private static void BuildSource(SourceBuilder builder, TypeModel type)
    {
        var containingTypes = type.ContainingTypes;
        var properties = type.Properties;

        builder.AutoGenerated();
        builder.EnableNullable();
        builder.NewLine();

        if (!String.IsNullOrEmpty(type.Namespace))
        {
            builder.Namespace(type.Namespace);
            builder.NewLine();
        }

        foreach (var ct in containingTypes)
        {
            builder.Indent()
                .Append("partial ")
                .Append(ct.IsValueType ? "struct " : "class ")
                .Append(ct.ClassName)
                .NewLine();
            builder.BeginScope();
        }

        builder.Indent()
            .Append("partial ")
            .Append(type.IsValueType ? "struct " : "class ")
            .Append(type.ClassName)
            .Append(" : global::System.IEquatable<")
            .Append(type.ClassName)
            .Append(">")
            .NewLine();
        builder.BeginScope();

        // Equals(object?)
        builder.Indent()
            .Append("public override bool Equals(object? obj) => obj is ")
            .Append(type.ClassName)
            .Append(" other && Equals(other);")
            .NewLine();
        builder.NewLine();

        // Equals(T) for value types, Equals(T?) for reference types
        builder.Indent()
            .Append("public bool Equals(")
            .Append(type.ClassName)
            .Append(type.IsValueType ? " other)" : "? other)")
            .NewLine();
        builder.BeginScope();

        if (!type.IsValueType)
        {
            builder.Indent().Append("if (other is null)").NewLine();
            builder.BeginScope();
            builder.Indent().Append("return false;").NewLine();
            builder.EndScope();

            builder.Indent().Append("if (global::System.Object.ReferenceEquals(this, other))").NewLine();
            builder.BeginScope();
            builder.Indent().Append("return true;").NewLine();
            builder.EndScope();

            if (!type.IsSealed)
            {
                builder.Indent().Append("if (this.GetType() != other.GetType())").NewLine();
                builder.BeginScope();
                builder.Indent().Append("return false;").NewLine();
                builder.EndScope();
            }
        }

        builder.Indent().Append("return ");

        for (var i = 0; i < properties.Count; i++)
        {
            var prop = properties[i];
            if (i > 0)
            {
                builder.Indent().Append("    ");
            }

            if (prop.Collection != CollectionKind.None && type.DeepCollectionEquality)
            {
                builder
                    .Append(prop.Collection == CollectionKind.Unordered ? "UnorderedEqualOrBothNull(this." : "SequenceEqualOrBothNull(this.")
                    .Append(prop.Name)
                    .Append(", other.")
                    .Append(prop.Name)
                    .Append(")");
            }
            else
            {
                builder
                    .Append("global::System.Collections.Generic.EqualityComparer<")
                    .Append(prop.TypeName)
                    .Append(">.Default.Equals(this.")
                    .Append(prop.Name)
                    .Append(", other.")
                    .Append(prop.Name)
                    .Append(")");
            }

            if (i < (properties.Count - 1))
            {
                builder.Append(" &&").NewLine();
            }
        }

        builder.Append(";").NewLine();
        builder.EndScope();
        builder.NewLine();

        // GetHashCode
        builder.Indent().Append("public override int GetHashCode()").NewLine();
        builder.BeginScope();
        builder.Indent().Append("var hash = new global::System.HashCode();").NewLine();
        foreach (var prop in properties)
        {
            if (prop.Collection == CollectionKind.Sequence && type.DeepCollectionEquality)
            {
                builder.Indent().Append("if (this.").Append(prop.Name).Append(" is not null)").NewLine();
                builder.BeginScope();
                builder.Indent().Append("foreach (var item in this.").Append(prop.Name).Append(")").NewLine();
                builder.BeginScope();
                builder.Indent().Append("hash.Add(item);").NewLine();
                builder.EndScope();
                builder.EndScope();
            }
            else if (prop.Collection == CollectionKind.Unordered && type.DeepCollectionEquality)
            {
                builder.Indent().Append("if (this.").Append(prop.Name).Append(" is not null)").NewLine();
                builder.BeginScope();
                builder.Indent().Append("hash.Add(UnorderedHash(this.").Append(prop.Name).Append("));").NewLine();
                builder.EndScope();
            }
            else
            {
                builder.Indent().Append("hash.Add(this.").Append(prop.Name).Append(");").NewLine();
            }
        }
        builder.Indent().Append("return hash.ToHashCode();").NewLine();
        builder.EndScope();

        // Operators
        if (type.GenerateOperators)
        {
            builder.NewLine();
            if (type.IsValueType)
            {
                builder.Indent()
                    .Append("public static bool operator ==(")
                    .Append(type.ClassName)
                    .Append(" left, ")
                    .Append(type.ClassName)
                    .Append(" right) => left.Equals(right);")
                    .NewLine();
                builder.NewLine();
                builder.Indent()
                    .Append("public static bool operator !=(")
                    .Append(type.ClassName)
                    .Append(" left, ")
                    .Append(type.ClassName)
                    .Append(" right) => !left.Equals(right);")
                    .NewLine();
            }
            else
            {
                builder.Indent()
                    .Append("public static bool operator ==(")
                    .Append(type.ClassName)
                    .Append("? left, ")
                    .Append(type.ClassName)
                    .Append("? right) =>")
                    .NewLine();
                builder.Indent()
                    .Append("    global::System.Object.ReferenceEquals(left, right) || (left is not null && left.Equals(right));")
                    .NewLine();
                builder.NewLine();
                builder.Indent()
                    .Append("public static bool operator !=(")
                    .Append(type.ClassName)
                    .Append("? left, ")
                    .Append(type.ClassName)
                    .Append("? right) => !(left == right);")
                    .NewLine();
            }
        }

        // Comparison helpers
        if (type.DeepCollectionEquality && properties.Any(static x => x.Collection == CollectionKind.Sequence))
        {
            builder.NewLine();
            builder.Indent()
                .Append("private static bool SequenceEqualOrBothNull<T>(")
                .NewLine();
            builder.Indent()
                .Append("    global::System.Collections.Generic.IEnumerable<T>? a,")
                .NewLine();
            builder.Indent()
                .Append("    global::System.Collections.Generic.IEnumerable<T>? b)")
                .NewLine();
            builder.BeginScope();
            builder.Indent().Append("if (a is null)").NewLine();
            builder.BeginScope();
            builder.Indent().Append("return b is null;").NewLine();
            builder.EndScope();
            builder.Indent().Append("if (b is null)").NewLine();
            builder.BeginScope();
            builder.Indent().Append("return false;").NewLine();
            builder.EndScope();
            builder.Indent()
                .Append("return global::System.Linq.Enumerable.SequenceEqual(a, b);")
                .NewLine();
            builder.EndScope();
        }

        if (type.DeepCollectionEquality && properties.Any(static x => x.Collection == CollectionKind.Unordered))
        {
            // Multiset comparison over the enumerated element (T for a set, KeyValuePair<K,V>
            // for a dictionary), so order and rehash history do not affect the result.
            builder.NewLine();
            builder.Indent()
                .Append("private static bool UnorderedEqualOrBothNull<T>(")
                .NewLine();
            builder.Indent()
                .Append("    global::System.Collections.Generic.IEnumerable<T>? a,")
                .NewLine();
            builder.Indent()
                .Append("    global::System.Collections.Generic.IEnumerable<T>? b)")
                .NewLine();
            builder.BeginScope();
            builder.Indent().Append("if (a is null)").NewLine();
            builder.BeginScope();
            builder.Indent().Append("return b is null;").NewLine();
            builder.EndScope();
            builder.Indent().Append("if (b is null)").NewLine();
            builder.BeginScope();
            builder.Indent().Append("return false;").NewLine();
            builder.EndScope();
            // ValueTuple<T> is a struct key delegating to EqualityComparer<T>.Default, which
            // satisfies the Dictionary notnull constraint and accepts null elements, keeping
            // the comparison O(n + m) even for large sets.
            builder.Indent().Append("var counts = new global::System.Collections.Generic.Dictionary<global::System.ValueTuple<T>, int>();").NewLine();
            builder.Indent().Append("var balance = 0;").NewLine();
            builder.Indent().Append("foreach (var item in a)").NewLine();
            builder.BeginScope();
            builder.Indent().Append("counts.TryGetValue(global::System.ValueTuple.Create(item), out var count);").NewLine();
            builder.Indent().Append("counts[global::System.ValueTuple.Create(item)] = count + 1;").NewLine();
            builder.Indent().Append("balance++;").NewLine();
            builder.EndScope();
            builder.Indent().Append("foreach (var item in b)").NewLine();
            builder.BeginScope();
            builder.Indent().Append("if (!counts.TryGetValue(global::System.ValueTuple.Create(item), out var count) || (count == 0))").NewLine();
            builder.BeginScope();
            builder.Indent().Append("return false;").NewLine();
            builder.EndScope();
            builder.Indent().Append("counts[global::System.ValueTuple.Create(item)] = count - 1;").NewLine();
            builder.Indent().Append("balance--;").NewLine();
            builder.EndScope();
            builder.Indent().Append("return balance == 0;").NewLine();
            builder.EndScope();

            // Commutative, unchecked accumulation: equal contents hash equally regardless of
            // order, and the sum must not throw in projects that compile with checked arithmetic.
            builder.NewLine();
            builder.Indent()
                .Append("private static int UnorderedHash<T>(global::System.Collections.Generic.IEnumerable<T> source)")
                .NewLine();
            builder.BeginScope();
            builder.Indent().Append("var hash = 0;").NewLine();
            builder.Indent().Append("foreach (var item in source)").NewLine();
            builder.BeginScope();
            builder.Indent().Append("hash = unchecked(hash + (item is null ? 0 : global::System.Collections.Generic.EqualityComparer<T>.Default.GetHashCode(item)));").NewLine();
            builder.EndScope();
            builder.Indent().Append("return hash;").NewLine();
            builder.EndScope();
        }

        builder.EndScope();

        for (var i = 0; i < containingTypes.Count; i++)
        {
            builder.EndScope();
        }
    }

    // ------------------------------------------------------------
    // Helper
    // ------------------------------------------------------------

    private static string MakeFilename(string ns, EquatableArray<ContainingTypeModel> containingTypes, string className, string suffix)
    {
        var buffer = new StringBuilder();
        if (!String.IsNullOrEmpty(ns))
        {
            buffer.Append(ns.Replace('.', '_'));
            buffer.Append('_');
        }
        foreach (var ct in containingTypes)
        {
            buffer.Append(ct.ClassName.Replace('<', '[').Replace('>', ']'));
            buffer.Append('_');
        }
        buffer.Append(className.Replace('<', '[').Replace('>', ']'));
        buffer.Append('_');
        buffer.Append(suffix);
        buffer.Append(".g.cs");
        return buffer.ToString();
    }

    // ------------------------------------------------------------
    // Model
    // ------------------------------------------------------------

    private sealed record ContainingTypeModel(
        string ClassName,
        bool IsValueType);

    private enum CollectionKind
    {
        None,
        Sequence,
        Unordered
    }

    private sealed record PropertyModel(
        string Name,
        CollectionKind Collection,
        string TypeName);

    private sealed record TypeModel(
        string Namespace,
        EquatableArray<ContainingTypeModel> ContainingTypes,
        string ClassName,
        bool IsValueType,
        bool IsSealed,
        bool GenerateOperators,
        bool DeepCollectionEquality,
        EquatableArray<PropertyModel> Properties);
}
