namespace BunnyTail.CommonCode.Generator;

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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
                static (node, _) => IsTypeSyntax(node),
                static (ctx, _) => GetTypeModel(ctx))
            .Collect();
        var treeProvider = context.ForAttributeWithMetadataNameSyntaxTrees(
            GenerateAttributeName,
            static (node, _) => IsTypeSyntax(node));

        context.RegisterSourceOutput(
            targetProvider.Combine(treeProvider),
            static (spc, input) => spc.ReportDiagnostics(GeneratedTypes.SelectDiagnostics(input.Left, "GenerateEquality").Distinct(), input.Right));

        var models = targetProvider
            .SelectMany(static (x, _) => GeneratedTypes.SelectTypes(x))
            .WithTrackingName("Models");
        context.RegisterImplementationSourceOutput(
            models,
            static (spc, type) => Execute(spc, type));
    }

    private static bool IsTypeSyntax(SyntaxNode node) =>
        node is ClassDeclarationSyntax or StructDeclarationSyntax or RecordDeclarationSyntax;

    private static Result<TypeModel> GetTypeModel(GeneratorAttributeSyntaxContext context)
    {
        var syntax = (TypeDeclarationSyntax)context.TargetNode;
        var symbol = (INamedTypeSymbol)context.TargetSymbol;

        if (!GeneratedTypes.IsExtendable(syntax, symbol))
        {
            return Results.Error<TypeModel>(new DiagnosticInfo(Diagnostics.EqualityInvalidTypeDefinition, syntax.Identifier.GetLocation(), symbol.Name));
        }

        var ns = String.IsNullOrEmpty(symbol.ContainingNamespace.Name) ? string.Empty : symbol.ContainingNamespace.ToDisplayString();

        var containingTypes = symbol.GetContainingTypes()
            .Select(static x => new ContainingTypeModel(x.GetClassName(), x.GetDeclarationKeyword()))
            .ToArray();

        var attr = context.Attributes[0];

        var generateOperators = GetBoolArg(attr, nameof(TypeModel.GenerateOperators)) ?? true;
        var deepCollectionEquality = GetBoolArg(attr, nameof(TypeModel.DeepCollectionEquality)) ?? false;

        var compilation = context.SemanticModel.Compilation;
        var properties = new List<PropertyModel>();
        var diagnostics = new List<DiagnosticInfo>();
        foreach (var member in MemberCollector.GetInstanceMembers(symbol))
        {
            if ((member is not IPropertySymbol property) || (property.DeclaredAccessibility != Accessibility.Public))
            {
                continue;
            }

            if ((property.GetMethod is null) || !compilation.IsSymbolAccessibleWithin(property.GetMethod, symbol))
            {
                continue;
            }

            if (property.HasAttribute(IgnoreAttributeName))
            {
                continue;
            }

            if (property.Type.IsRefLikeType)
            {
                diagnostics.Add(new DiagnosticInfo(
                    Diagnostics.EqualityRefStructMember,
                    property.Locations.FirstOrDefault(static x => x.IsInSource) ?? syntax.Identifier.GetLocation(),
                    property.Name,
                    property.Type.ToDisplayString()));
                continue;
            }

            properties.Add(new PropertyModel(
                property.Name,
                ClassifyCollection(property.Type),
                property.Type.ToDisplayString(SymbolDisplayFormats.FullyQualifiedNullable)));
        }

        if (properties.Count == 0)
        {
            return diagnostics.Count > 0
                ? Results.Errors<TypeModel>(diagnostics)
                : Results.Error<TypeModel>(new DiagnosticInfo(Diagnostics.EqualityNoProperties, syntax.Identifier.GetLocation(), symbol.Name));
        }

        var className = symbol.GetClassName();
        return new Result<TypeModel>(
            new TypeModel(
                ns,
                new EquatableArray<ContainingTypeModel>(containingTypes),
                className,
                symbol.GetDeclarationKeyword(),
                symbol.IsValueType,
                symbol.IsRefLikeType,
                symbol.IsRecord,
                symbol.IsSealed,
                generateOperators,
                deepCollectionEquality,
                new EquatableArray<PropertyModel>(properties),
                HintNameBuilder.Build(ns, [.. containingTypes.Select(static x => x.ClassName), className, "Equality"]),
                symbol.ToDisplayString()),
            new EquatableArray<DiagnosticInfo>(diagnostics));
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

            if (named.HasFullyQualifiedMetadataName("System.Collections.Generic.ISet`1") ||
                named.HasFullyQualifiedMetadataName("System.Collections.Generic.IReadOnlySet`1") ||
                named.HasFullyQualifiedMetadataName("System.Collections.Generic.IDictionary`2") ||
                named.HasFullyQualifiedMetadataName("System.Collections.Generic.IReadOnlyDictionary`2"))
            {
                return CollectionKind.Unordered;
            }

            if (named.HasFullyQualifiedMetadataName("System.Collections.Generic.IEnumerable`1"))
            {
                isEnumerable = true;
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

    private static void Execute(SourceProductionContext context, TypeModel type)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        var builder = new SourceBuilder();
        BuildSource(builder, type);

        context.AddSource(type.HintName, builder);
    }

    private static void BuildSource(SourceBuilder builder, TypeModel type)
    {
        var containingTypes = type.ContainingTypes;
        var properties = type.Properties;

        builder.AutoGenerated();
        builder.EnableNullable();
        builder.Disable("CS0612, CS0618");
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
                .Append(ct.Keyword)
                .Append(" ")
                .Append(ct.ClassName)
                .NewLine();
            builder.BeginScope();
        }

        builder.Indent()
            .Append("partial ")
            .Append(type.Keyword)
            .Append(" ")
            .Append(type.ClassName);
        if (!type.IsRecord && !type.IsRefLike)
        {
            builder
                .Append(" : global::System.IEquatable<")
                .Append(type.ClassName)
                .Append(">");
        }

        builder.NewLine();
        builder.BeginScope();

        // Equals(object?)
        if (type.IsRefLike)
        {
            builder.Indent().Append("public override bool Equals(object? obj) => false;").NewLine();
            builder.NewLine();
        }
        else if (!type.IsRecord)
        {
            builder.Indent()
                .Append("public override bool Equals(object? obj) => obj is ")
                .Append(type.ClassName)
                .Append(" other && Equals(other);")
                .NewLine();
            builder.NewLine();
        }

        // Equals(T) for value types, Equals(T?) for reference types
        builder.Indent()
            .Append(type.IsRecord && !type.IsValueType && !type.IsSealed ? "public virtual bool Equals(" : "public bool Equals(")
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
                builder.Indent().Append(type.IsRecord ? "if (this.EqualityContract != other.EqualityContract)" : "if (this.GetType() != other.GetType())").NewLine();
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

            var name = CSharpIdentifier.Escape(prop.Name);
            if ((prop.Collection != CollectionKind.None) && type.DeepCollectionEquality)
            {
                builder
                    .Append(prop.Collection == CollectionKind.Unordered ? "__UnorderedEqualOrBothNull(this." : "__SequenceEqualOrBothNull(this.")
                    .Append(name)
                    .Append(", other.")
                    .Append(name)
                    .Append(")");
            }
            else
            {
                builder
                    .Append("global::System.Collections.Generic.EqualityComparer<")
                    .Append(prop.TypeName)
                    .Append(">.Default.Equals(this.")
                    .Append(name)
                    .Append(", other.")
                    .Append(name)
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
            var name = CSharpIdentifier.Escape(prop.Name);
            if ((prop.Collection == CollectionKind.Sequence) && type.DeepCollectionEquality)
            {
                builder.Indent().Append("if (this.").Append(name).Append(" is not null)").NewLine();
                builder.BeginScope();
                builder.Indent().Append("foreach (var item in this.").Append(name).Append(")").NewLine();
                builder.BeginScope();
                builder.Indent().Append("hash.Add(item);").NewLine();
                builder.EndScope();
                builder.EndScope();
            }
            else if ((prop.Collection == CollectionKind.Unordered) && type.DeepCollectionEquality)
            {
                builder.Indent().Append("if (this.").Append(name).Append(" is not null)").NewLine();
                builder.BeginScope();
                builder.Indent().Append("hash.Add(__UnorderedHash(this.").Append(name).Append("));").NewLine();
                builder.EndScope();
            }
            else
            {
                builder.Indent().Append("hash.Add(this.").Append(name).Append(");").NewLine();
            }
        }
        builder.Indent().Append("return hash.ToHashCode();").NewLine();
        builder.EndScope();

        // Operators
        if (type.GenerateOperators && !type.IsRecord)
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
                .Append("private static bool __SequenceEqualOrBothNull<__T>(")
                .NewLine();
            builder.Indent()
                .Append("    global::System.Collections.Generic.IEnumerable<__T>? a,")
                .NewLine();
            builder.Indent()
                .Append("    global::System.Collections.Generic.IEnumerable<__T>? b)")
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
            // Comparison helpers
            builder.NewLine();
            builder.Indent()
                .Append("private static bool __UnorderedEqualOrBothNull<__T>(")
                .NewLine();
            builder.Indent()
                .Append("    global::System.Collections.Generic.IEnumerable<__T>? a,")
                .NewLine();
            builder.Indent()
                .Append("    global::System.Collections.Generic.IEnumerable<__T>? b)")
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

            // Unordered comparison
            builder.Indent().Append("var counts = new global::System.Collections.Generic.Dictionary<global::System.ValueTuple<__T>, int>();").NewLine();
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

            // Unordered hash helper
            builder.NewLine();
            builder.Indent()
                .Append("private static int __UnorderedHash<__T>(global::System.Collections.Generic.IEnumerable<__T> source)")
                .NewLine();
            builder.BeginScope();
            builder.Indent().Append("var hash = 0;").NewLine();
            builder.Indent().Append("foreach (var item in source)").NewLine();
            builder.BeginScope();
            builder.Indent().Append("hash = unchecked(hash + (item is null ? 0 : global::System.Collections.Generic.EqualityComparer<__T>.Default.GetHashCode(item)));").NewLine();
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
    // Model
    // ------------------------------------------------------------

    private sealed record ContainingTypeModel(
        string ClassName,
        string Keyword);

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
        string Keyword,
        bool IsValueType,
        bool IsRefLike,
        bool IsRecord,
        bool IsSealed,
        bool GenerateOperators,
        bool DeepCollectionEquality,
        EquatableArray<PropertyModel> Properties,
        string HintName,
        string DisplayName) : IGeneratedType;
}
