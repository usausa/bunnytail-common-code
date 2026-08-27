namespace BunnyTail.CommonCode.Generator;

using System;
using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using SourceGenerateHelper;

[Generator]
public sealed class DeepCloneGenerator : IIncrementalGenerator
{
    // ReSharper disable InconsistentNaming
    private const string GenerateAttributeName = "BunnyTail.CommonCode.GenerateDeepCloneAttribute";
    private const string ShallowCloneAttributeName = "BunnyTail.CommonCode.ShallowCloneAttribute";
    private const string IgnoreCloneAttributeName = "BunnyTail.CommonCode.IgnoreCloneAttribute";
    private const string IDeepCloneableName = "BunnyTail.CommonCode.IDeepCloneable<T>";
    // ReSharper restore InconsistentNaming

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
            return Results.Error<TypeModel>(new DiagnosticInfo(Diagnostics.DeepCloneInvalidTypeDefinition, syntax.Identifier.GetLocation(), symbol.Name));
        }

        // Check whether IDeepCloneable<T> is implemented
        var implementsDeepCloneable = symbol.AllInterfaces.Any(static x =>
            x.IsGenericType && x.ConstructedFrom.ToDisplayString() == IDeepCloneableName);
        if (!implementsDeepCloneable)
        {
            return Results.Error<TypeModel>(new DiagnosticInfo(Diagnostics.DeepCloneNotImplementIDeepCloneable, syntax.Identifier.GetLocation(), symbol.Name));
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

        var properties = new List<PropertyModel>();
        var diagnostics = new List<DiagnosticInfo>();
        foreach (var member in symbol.GetMembers().OfType<IPropertySymbol>())
        {
            // Exclude indexers and non-public properties
            if (member.IsIndexer || (member.DeclaredAccessibility != Accessibility.Public))
            {
                continue;
            }

            if ((member.GetMethod is null) || (member.SetMethod is null))
            {
                continue;
            }

            if (member.GetAttributes().Any(static x => x.AttributeClass?.ToDisplayString() == IgnoreCloneAttributeName))
            {
                continue;
            }

            var shallow = member.GetAttributes().Any(static x => x.AttributeClass?.ToDisplayString() == ShallowCloneAttributeName);
            var cloneStrategy = shallow ? CloneStrategy.Shallow : GetCloneStrategy(member.Type);

            if (!shallow && (cloneStrategy == CloneStrategy.Unknown))
            {
                // Reference types with no known deep-clone method fall back to a shallow copy, but notify the user via a diagnostic
                diagnostics.Add(new DiagnosticInfo(
                    Diagnostics.DeepClonePropertyMissingDeepClone,
                    member.Locations.FirstOrDefault() ?? syntax.GetLocation(),
                    member.Name,
                    member.Type.ToDisplayString()));
                cloneStrategy = CloneStrategy.Shallow;
            }

            properties.Add(new PropertyModel(
                member.Name,
                member.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                cloneStrategy,
                member.Type.IsReferenceType,
                member.SetMethod.IsInitOnly));
        }

        return new Result<TypeModel>(
            new TypeModel(
                ns,
                new EquatableArray<ContainingTypeModel>(containingTypes ?? []),
                symbol.GetClassName(),
                symbol.IsValueType,
                new EquatableArray<PropertyModel>(properties)),
            new EquatableArray<DiagnosticInfo>(diagnostics));
    }

    private static CloneStrategy GetCloneStrategy(ITypeSymbol typeSymbol)
    {
        if (typeSymbol.IsValueType || (typeSymbol.SpecialType == SpecialType.System_String))
        {
            return CloneStrategy.Direct;
        }

        if (typeSymbol.AllInterfaces.Any(static x => x.IsGenericType && x.ConstructedFrom.ToDisplayString() == IDeepCloneableName))
        {
            return CloneStrategy.DeepClone;
        }

        if (typeSymbol is IArrayTypeSymbol)
        {
            return CloneStrategy.Array;
        }

        if (typeSymbol is INamedTypeSymbol named)
        {
            var fullName = named.ConstructedFrom.ToDisplayString();
            if (fullName == "System.Collections.Generic.List<T>")
            {
                return CloneStrategy.List;
            }
        }

        return CloneStrategy.Unknown;
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

        context.AddSource(
            HintNameBuilder.Build(type.Namespace, [.. type.ContainingTypes.Select(static x => x.ClassName), type.ClassName, "DeepClone"]),
            builder);
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
            .NewLine();
        builder.BeginScope();

        // DeepClone()
        builder.Indent()
            .Append("public ")
            .Append(type.ClassName)
            .Append(" DeepClone()")
            .NewLine();
        builder.BeginScope();

        builder.Indent().Append("var clone = new ").Append(type.ClassName);

        var hasInit = false;
        foreach (var prop in properties)
        {
            if (!prop.RequiresInit)
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

            builder.Indent().Append(prop.Name).Append(" = ");
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
            builder.Append("();").NewLine();
        }

        // Settable properties are set via assignment
        foreach (var prop in properties)
        {
            if (prop.RequiresInit)
            {
                continue;
            }

            builder.Indent().Append("clone.").Append(prop.Name).Append(" = ");
            BuildCloneExpression(builder, prop);
            builder.Append(";").NewLine();
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
        switch (prop.Strategy)
        {
            case CloneStrategy.DeepClone:
                if (prop.IsReferenceType)
                {
                    builder
                        .Append("this.").Append(prop.Name)
                        .Append(" is null ? null! : this.").Append(prop.Name).Append(".DeepClone()");
                }
                else
                {
                    builder.Append("this.").Append(prop.Name).Append(".DeepClone()");
                }
                break;

            case CloneStrategy.Array:
                if (prop.IsReferenceType)
                {
                    builder
                        .Append("this.").Append(prop.Name)
                        .Append(" is null ? null! : (")
                        .Append(prop.TypeName)
                        .Append(")((global::System.Array)this.")
                        .Append(prop.Name)
                        .Append(").Clone()");
                }
                else
                {
                    builder
                        .Append("(")
                        .Append(prop.TypeName)
                        .Append(")((global::System.Array)this.")
                        .Append(prop.Name)
                        .Append(").Clone()");
                }
                break;

            case CloneStrategy.List:
                if (prop.IsReferenceType)
                {
                    builder
                        .Append("this.").Append(prop.Name)
                        .Append(" is null ? null! : new ")
                        .Append(prop.TypeName)
                        .Append("(this.").Append(prop.Name).Append(")");
                }
                else
                {
                    builder
                        .Append("new ").Append(prop.TypeName)
                        .Append("(this.").Append(prop.Name).Append(")");
                }
                break;

            case CloneStrategy.Direct:
            case CloneStrategy.Shallow:
            default:
                builder.Append("this.").Append(prop.Name);
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
        bool IsValueType);

    private sealed record PropertyModel(
        string Name,
        string TypeName,
        CloneStrategy Strategy,
        bool IsReferenceType,
        bool RequiresInit);

    private sealed record TypeModel(
        string Namespace,
        EquatableArray<ContainingTypeModel> ContainingTypes,
        string ClassName,
        bool IsValueType,
        EquatableArray<PropertyModel> Properties);
}
