namespace BunnyTail.CommonCode.Generator;

using System;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using SourceGenerateHelper;

[Generator]
public sealed class DeepCloneGenerator : IIncrementalGenerator
{
    // ReSharper disable InconsistentNaming
    private const string GenerateAttributeName = "BunnyTail.CommonCode.GenerateDeepCloneAttribute";
    private const string ShallowCloneAttributeName = "BunnyTail.CommonCode.ShallowCloneAttribute";
    private const string IgnoreCloneAttributeName = "BunnyTail.CommonCode.IgnoreCloneAttribute";
    private const string IDeepCloneableName = "BunnyTail.CommonCode.IDeepCloneable`1";
    // ReSharper restore InconsistentNaming

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
            static (spc, input) => spc.ReportDiagnostics(GeneratedTypes.SelectDiagnostics(input.Left, "GenerateDeepClone").Distinct(), input.Right));

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
            return Results.Error<TypeModel>(new DiagnosticInfo(Diagnostics.DeepCloneInvalidTypeDefinition, syntax.Identifier.GetLocation(), symbol.Name));
        }

        if (symbol.IsRefLikeType)
        {
            return Results.Error<TypeModel>(new DiagnosticInfo(Diagnostics.DeepCloneRefStruct, syntax.Identifier.GetLocation(), symbol.Name));
        }

        // Check whether IDeepCloneable<T> is implemented
        var implementsDeepCloneable = symbol.AllInterfaces.Any(static x => x.HasFullyQualifiedMetadataName(IDeepCloneableName));
        if (!implementsDeepCloneable)
        {
            return Results.Error<TypeModel>(new DiagnosticInfo(Diagnostics.DeepCloneNotImplementIDeepCloneable, syntax.Identifier.GetLocation(), symbol.Name));
        }

        var ns = String.IsNullOrEmpty(symbol.ContainingNamespace.Name) ? string.Empty : symbol.ContainingNamespace.ToDisplayString();

        var containingTypes = symbol.GetContainingTypes()
            .Select(static x => new ContainingTypeModel(x.GetClassName(), x.GetDeclarationKeyword()))
            .ToArray();

        var compilation = context.SemanticModel.Compilation;
        var properties = new List<PropertyModel>();
        var diagnostics = new List<DiagnosticInfo>();
        foreach (var member in MemberCollector.GetInstanceMembers(symbol))
        {
            // Exclude fields and non-public properties
            if ((member is not IPropertySymbol property) || (property.DeclaredAccessibility != Accessibility.Public))
            {
                continue;
            }

            if ((property.GetMethod is null) || !compilation.IsSymbolAccessibleWithin(property.GetMethod, symbol))
            {
                continue;
            }

            if (property.HasAttribute(IgnoreCloneAttributeName))
            {
                continue;
            }

            var shallow = property.HasAttribute(ShallowCloneAttributeName);
            var typeName = property.Type.WithNullableAnnotation(NullableAnnotation.NotAnnotated).ToDisplayString(SymbolDisplayFormats.FullyQualifiedNullable);
            var canBeNull = property.Type.CanBeNull();

            if ((property.SetMethod is null) || !compilation.IsSymbolAccessibleWithin(property.SetMethod, symbol))
            {
                if (!shallow && !symbol.IsRecord && IsList(property.Type))
                {
                    properties.Add(new PropertyModel(property.Name, typeName, CloneStrategy.List, canBeNull, false, true));
                }

                continue;
            }

            var cloneStrategy = shallow ? CloneStrategy.Shallow : GetCloneStrategy(property.Type, compilation);

            if (!shallow && (cloneStrategy == CloneStrategy.Unknown))
            {
                // Reference types with no known deep-clone method fall back to a shallow copy, but notify the user via a diagnostic
                if (SymbolEqualityComparer.Default.Equals(property.ContainingType, symbol))
                {
                    diagnostics.Add(new DiagnosticInfo(
                        Diagnostics.DeepClonePropertyMissingDeepClone,
                        property.Locations.FirstOrDefault() ?? syntax.GetLocation(),
                        property.Name,
                        property.Type.ToDisplayString()));
                }

                cloneStrategy = CloneStrategy.Shallow;
            }

            properties.Add(new PropertyModel(
                property.Name,
                typeName,
                cloneStrategy,
                canBeNull,
                property.SetMethod.IsInitOnly || property.IsRequired,
                false));
        }

        var className = symbol.GetClassName();
        return new Result<TypeModel>(
            new TypeModel(
                ns,
                new EquatableArray<ContainingTypeModel>(containingTypes),
                className,
                symbol.GetDeclarationKeyword(),
                symbol.IsRecord,
                GetMethodModifier(symbol),
                new EquatableArray<PropertyModel>(properties),
                HintNameBuilder.Build(ns, [.. containingTypes.Select(static x => x.ClassName), className, "DeepClone"]),
                symbol.ToDisplayString()),
            new EquatableArray<DiagnosticInfo>(diagnostics));
    }

    private static CloneStrategy GetCloneStrategy(ITypeSymbol typeSymbol, Compilation compilation)
    {
        if (typeSymbol.IsValueType || (typeSymbol.SpecialType == SpecialType.System_String))
        {
            return CloneStrategy.Direct;
        }

        if (IsDeepCloneable(typeSymbol, compilation))
        {
            return CloneStrategy.DeepClone;
        }

        if (typeSymbol is IArrayTypeSymbol)
        {
            return CloneStrategy.Array;
        }

        if (IsList(typeSymbol))
        {
            return CloneStrategy.List;
        }

        return CloneStrategy.Unknown;
    }

    private static bool IsDeepCloneable(ITypeSymbol type, Compilation compilation) =>
        GetInterfaces(type).Any(x => x.HasFullyQualifiedMetadataName(IDeepCloneableName) &&
                                     compilation.ClassifyCommonConversion(x.TypeArguments[0], type).IsImplicit);

    private static IEnumerable<INamedTypeSymbol> GetInterfaces(ITypeSymbol type) =>
        type switch
        {
            ITypeParameterSymbol typeParameter => typeParameter.ConstraintTypes.SelectMany(GetInterfaces),
            INamedTypeSymbol { TypeKind: TypeKind.Interface } named => named.AllInterfaces.Add(named),
            _ => type.AllInterfaces
        };

    private static bool IsList(ITypeSymbol type) =>
        type.HasFullyQualifiedMetadataName("System.Collections.Generic.List`1");

    private static string GetMethodModifier(INamedTypeSymbol symbol)
    {
        if (symbol.IsValueType)
        {
            return string.Empty;
        }

        for (var current = symbol.BaseType; current is not null; current = current.BaseType)
        {
            var existing = current.GetMembers("DeepClone")
                .OfType<IMethodSymbol>()
                .FirstOrDefault(static x => !x.IsStatic && (x.Arity == 0) && (x.Parameters.Length == 0) && (x.DeclaredAccessibility != Accessibility.Private));
            if (existing is not null)
            {
                return (existing.IsVirtual || existing.IsOverride || existing.IsAbstract) && !existing.IsSealed ? "override " : "new ";
            }

            if (current.HasAttribute(GenerateAttributeName))
            {
                return "override ";
            }
        }

        return symbol.IsSealed ? string.Empty : "virtual ";
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
            .Append(type.ClassName)
            .NewLine();
        builder.BeginScope();

        // DeepClone()
        builder.Indent()
            .Append("public ")
            .Append(type.Modifier)
            .Append(type.ClassName)
            .Append(" DeepClone()")
            .NewLine();
        builder.BeginScope();

        builder.Indent().Append("var clone = ");
        if (type.IsRecord)
        {
            builder.Append("this with");
        }
        else
        {
            builder.Append("new ").Append(type.ClassName);
        }

        var hasInit = false;
        foreach (var prop in properties)
        {
            if (!type.IsRecord && !prop.RequiresInit)
            {
                continue;
            }

            if (!hasInit)
            {
                builder.NewLine();
                builder.Indent().Append("{").NewLine();
                builder.IndentLevel++;
                hasInit = true;
            }

            builder.Indent().Append(CSharpIdentifier.Escape(prop.Name)).Append(" = ");
            BuildCloneExpression(builder, prop);
            builder.Append(",").NewLine();
        }

        if (hasInit)
        {
            builder.IndentLevel--;
            builder.Indent().Append("};").NewLine();
        }
        else
        {
            builder.Append(type.IsRecord ? " { };" : "();").NewLine();
        }

        // Settable properties are set via assignment
        foreach (var prop in properties)
        {
            if (type.IsRecord || prop.RequiresInit || prop.IsReadOnly)
            {
                continue;
            }

            builder.Indent().Append("clone.").Append(CSharpIdentifier.Escape(prop.Name)).Append(" = ");
            BuildCloneExpression(builder, prop);
            builder.Append(";").NewLine();
        }

        foreach (var prop in properties.Where(static x => x.IsReadOnly))
        {
            var name = CSharpIdentifier.Escape(prop.Name);
            builder.Indent().Append("if ((this.").Append(name).Append(" is not null) && (clone.").Append(name).Append(" is not null))").NewLine();
            builder.BeginScope();
            builder.Indent().Append("clone.").Append(name).Append(".Clear();").NewLine();
            builder.Indent().Append("clone.").Append(name).Append(".AddRange(this.").Append(name).Append(");").NewLine();
            builder.EndScope();
        }

        builder.Indent().Append("return clone;").NewLine();

        builder.EndScope(); // DeepClone method

        builder.EndScope(); // class

        for (var i = 0; i < containingTypes.Count; i++)
        {
            builder.EndScope();
        }
    }

    private static void BuildCloneExpression(SourceBuilder builder, PropertyModel prop)
    {
        var name = CSharpIdentifier.Escape(prop.Name);
        switch (prop.Strategy)
        {
            case CloneStrategy.DeepClone:
                if (prop.CanBeNull)
                {
                    builder
                        .Append("this.").Append(name)
                        .Append(" is null ? default! : this.").Append(name).Append(".DeepClone()");
                }
                else
                {
                    builder.Append("this.").Append(name).Append(".DeepClone()");
                }
                break;

            case CloneStrategy.Array:
                if (prop.CanBeNull)
                {
                    builder
                        .Append("this.").Append(name)
                        .Append(" is null ? default! : (")
                        .Append(prop.TypeName)
                        .Append(")((global::System.Array)this.")
                        .Append(name)
                        .Append(").Clone()");
                }
                else
                {
                    builder
                        .Append("(")
                        .Append(prop.TypeName)
                        .Append(")((global::System.Array)this.")
                        .Append(name)
                        .Append(").Clone()");
                }
                break;

            case CloneStrategy.List:
                if (prop.CanBeNull)
                {
                    builder
                        .Append("this.").Append(name)
                        .Append(" is null ? default! : new ")
                        .Append(prop.TypeName)
                        .Append("(this.").Append(name).Append(")");
                }
                else
                {
                    builder
                        .Append("new ").Append(prop.TypeName)
                        .Append("(this.").Append(name).Append(")");
                }
                break;

            case CloneStrategy.Direct:
            case CloneStrategy.Shallow:
            default:
                builder.Append("this.").Append(name);
                break;
        }
    }

    // ------------------------------------------------------------
    // Model
    // ------------------------------------------------------------

    private enum CloneStrategy
    {
        Direct,
        DeepClone,
        Array,
        List,
        Shallow,
        Unknown
    }

    private sealed record ContainingTypeModel(
        string ClassName,
        string Keyword);

    private sealed record PropertyModel(
        string Name,
        string TypeName,
        CloneStrategy Strategy,
        bool CanBeNull,
        bool RequiresInit,
        bool IsReadOnly);

    private sealed record TypeModel(
        string Namespace,
        EquatableArray<ContainingTypeModel> ContainingTypes,
        string ClassName,
        string Keyword,
        bool IsRecord,
        string Modifier,
        EquatableArray<PropertyModel> Properties,
        string HintName,
        string DisplayName) : IGeneratedType;
}
