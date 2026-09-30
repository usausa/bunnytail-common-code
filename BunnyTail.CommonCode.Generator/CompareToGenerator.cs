namespace BunnyTail.CommonCode.Generator;

using System;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using SourceGenerateHelper;

[Generator]
public sealed class CompareToGenerator : IIncrementalGenerator
{
    private const string GenerateAttributeName = "BunnyTail.CommonCode.GenerateCompareToAttribute";
    private const string CompareKeyAttributeName = "BunnyTail.CommonCode.CompareKeyAttribute";

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
            static (spc, input) => spc.ReportDiagnostics(GeneratedTypes.SelectDiagnostics(input.Left, "GenerateCompareTo").Distinct(), input.Right));

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
            return Results.Error<TypeModel>(new DiagnosticInfo(Diagnostics.CompareToInvalidTypeDefinition, syntax.Identifier.GetLocation(), symbol.Name));
        }

        var ns = String.IsNullOrEmpty(symbol.ContainingNamespace.Name) ? string.Empty : symbol.ContainingNamespace.ToDisplayString();

        var containingTypes = symbol.GetContainingTypes()
            .Select(static x => new ContainingTypeModel(x.GetClassName(), x.GetDeclarationKeyword()))
            .ToArray();

        var generateOperators = GetBoolArg(context.Attributes[0], nameof(TypeModel.GenerateOperators)) ?? true;

        var compilation = context.SemanticModel.Compilation;
        var keys = new List<(int Order, string Name, string TypeName)>();
        var diagnostics = new List<DiagnosticInfo>();
        // ReSharper disable once LoopCanBeConvertedToQuery
        foreach (var member in MemberCollector.GetInstanceMembers(symbol))
        {
            // Exclude non-public members
            if (member.DeclaredAccessibility != Accessibility.Public)
            {
                continue;
            }

            var keyAttr = member.FindAttribute(CompareKeyAttributeName);
            if (keyAttr is null)
            {
                continue;
            }

            var type = member switch
            {
                IPropertySymbol { GetMethod: not null } property when compilation.IsSymbolAccessibleWithin(property.GetMethod, symbol) => property.Type,
                IFieldSymbol field => field.Type,
                _ => null
            };
            if (type is null)
            {
                continue;
            }

            if (!IsComparable(type, compilation))
            {
                diagnostics.Add(new DiagnosticInfo(
                    Diagnostics.CompareToKeyNotComparable,
                    member.Locations.FirstOrDefault(static x => x.IsInSource) ?? syntax.Identifier.GetLocation(),
                    member.Name,
                    type.ToDisplayString()));
                continue;
            }

            var order = GetIntArg(keyAttr, "Order") ?? 0;
            keys.Add((order, member.Name, type.ToDisplayString(SymbolDisplayFormats.FullyQualifiedNullable)));
        }

        if (keys.Count == 0)
        {
            return diagnostics.Count > 0
                ? Results.Errors<TypeModel>(diagnostics)
                : Results.Error<TypeModel>(new DiagnosticInfo(Diagnostics.CompareToNoKeys, syntax.Identifier.GetLocation(), symbol.Name));
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
                generateOperators,
                new EquatableArray<KeyModel>(keys.OrderBy(static k => k.Order).Select(static k => new KeyModel(k.Name, k.TypeName))),
                HintNameBuilder.Build(ns, [.. containingTypes.Select(static x => x.ClassName), className, "CompareTo"]),
                symbol.ToDisplayString()),
            new EquatableArray<DiagnosticInfo>(diagnostics));
    }

    private static bool IsComparable(ITypeSymbol type, Compilation compilation)
    {
        if (type.IsRefLikeType || (type.TypeKind is TypeKind.Pointer or TypeKind.FunctionPointer))
        {
            return false;
        }

        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
        {
            type = nullable.TypeArguments[0];
        }

        if ((type.TypeKind == TypeKind.TypeParameter) || (!type.IsValueType && !type.IsSealed && (type.TypeKind != TypeKind.Array)))
        {
            return true;
        }

        foreach (var iface in type.AllInterfaces)
        {
            if (iface.HasFullyQualifiedMetadataName("System.IComparable"))
            {
                return true;
            }

            if (iface.HasFullyQualifiedMetadataName("System.IComparable`1"))
            {
                var conversion = compilation.ClassifyCommonConversion(type, iface.TypeArguments[0]);
                if (conversion.IsIdentity || (conversion.IsImplicit && conversion.IsReference))
                {
                    return true;
                }
            }
        }

        return false;
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

    private static int? GetIntArg(AttributeData attr, string name)
    {
        var arg = attr.NamedArguments.FirstOrDefault(x => x.Key == name);
        if (arg.Value.IsNull)
        {
            return null;
        }

        if (arg.Value.Value is int i)
        {
            return i;
        }

        return null;
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
        var keys = type.Keys;

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
        if (!type.IsRefLike)
        {
            builder
                .Append(" : global::System.IComparable<")
                .Append(type.ClassName)
                .Append(">");
        }

        builder.NewLine();
        builder.BeginScope();

        // CompareTo(T) for value types, CompareTo(T?) for reference types
        builder.Indent()
            .Append("public int CompareTo(")
            .Append(type.ClassName)
            .Append(type.IsValueType ? " other)" : "? other)")
            .NewLine();
        builder.BeginScope();

        if (!type.IsValueType)
        {
            builder.Indent().Append("if (other is null)").NewLine();
            builder.BeginScope();
            builder.Indent().Append("return 1;").NewLine();
            builder.EndScope();
        }

        builder.Indent().Append("int result;").NewLine();
        foreach (var key in keys)
        {
            var name = CSharpIdentifier.Escape(key.Name);
            builder.Indent()
                .Append("result = global::System.Collections.Generic.Comparer<")
                .Append(key.TypeName)
                .Append(">.Default.Compare(this.")
                .Append(name)
                .Append(", other.")
                .Append(name)
                .Append(");")
                .NewLine();
            builder.Indent().Append("if (result != 0)").NewLine();
            builder.BeginScope();
            builder.Indent().Append("return result;").NewLine();
            builder.EndScope();
        }

        builder.Indent().Append("return 0;").NewLine();
        builder.EndScope();

        // Operators
        if (type.GenerateOperators)
        {
            builder.NewLine();
            builder.Indent()
                .Append("public static bool operator <(")
                .Append(type.ClassName)
                .Append(" left, ")
                .Append(type.ClassName)
                .Append(" right) => left.CompareTo(right) < 0;")
                .NewLine();
            builder.Indent()
                .Append("public static bool operator >(")
                .Append(type.ClassName)
                .Append(" left, ")
                .Append(type.ClassName)
                .Append(" right) => left.CompareTo(right) > 0;")
                .NewLine();
            builder.Indent()
                .Append("public static bool operator <=(")
                .Append(type.ClassName)
                .Append(" left, ")
                .Append(type.ClassName)
                .Append(" right) => left.CompareTo(right) <= 0;")
                .NewLine();
            builder.Indent()
                .Append("public static bool operator >=(")
                .Append(type.ClassName)
                .Append(" left, ")
                .Append(type.ClassName)
                .Append(" right) => left.CompareTo(right) >= 0;")
                .NewLine();
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

    private sealed record KeyModel(
        string Name,
        string TypeName);

    private sealed record TypeModel(
        string Namespace,
        EquatableArray<ContainingTypeModel> ContainingTypes,
        string ClassName,
        string Keyword,
        bool IsValueType,
        bool IsRefLike,
        bool GenerateOperators,
        EquatableArray<KeyModel> Keys,
        string HintName,
        string DisplayName) : IGeneratedType;
}
