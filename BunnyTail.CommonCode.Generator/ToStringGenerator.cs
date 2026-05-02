namespace BunnyTail.CommonCode.Generator;

using System;
using System.Collections.Immutable;
using System.Text;

using BunnyTail.CommonCode.Generator.Models;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

using SourceGenerateHelper;

[Generator]
public sealed class ToStringGenerator : IIncrementalGenerator
{
    private const string GenerateAttributeName = "BunnyTail.CommonCode.GenerateToStringAttribute";
    private const string IgnoreAttributeName = "BunnyTail.CommonCode.IgnoreToStringAttribute";

    private const string GenericEnumerableName = "System.Collections.Generic.IEnumerable<T>";

    // ------------------------------------------------------------
    // Initialize
    // ------------------------------------------------------------

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var optionProvider = context.AnalyzerConfigOptionsProvider
            .Select(static (provider, _) => GetOptions(provider));

        var targetProvider = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                GenerateAttributeName,
                static (node, _) => IsTypeSyntax(node),
                static (context, _) => GetTypeModel(context))
            .SelectMany(static (x, _) => x is not null ? ImmutableArray.Create(x) : [])
            .Collect();

        context.RegisterImplementationSourceOutput(
            optionProvider.Combine(targetProvider),
            static (spc, source) => Execute(spc, source.Left, source.Right));
    }

    private static GeneratorOptions GetOptions(AnalyzerConfigOptionsProvider provider)
    {
        var options = new GeneratorOptions();

        // Mode
        var mode = provider.GlobalOptions.GetValue<string?>("CommonCodeGeneratorToStringMode");
        if (String.IsNullOrEmpty(mode) || String.Equals(mode, "Default", StringComparison.OrdinalIgnoreCase))
        {
            options.OutputClassName = true;
        }

        // OutputClassName
        var outputClassName = provider.GlobalOptions.GetValue<bool?>("CommonCodeGeneratorToStringOutputClassName");
        if (outputClassName.HasValue)
        {
            options.OutputClassName = outputClassName.Value;
        }

        // NullLiteral
        var nullLiteral = provider.GlobalOptions.GetValue<string?>("CommonCodeGeneratorToStringNullLiteral");
        if (!String.IsNullOrEmpty(nullLiteral))
        {
            options.NullLiteral = nullLiteral;
        }

        return options;
    }

    private static bool IsTypeSyntax(SyntaxNode node) =>
        node is ClassDeclarationSyntax;

    private static Result<TypeModel> GetTypeModel(GeneratorAttributeSyntaxContext context)
    {
        var syntax = (ClassDeclarationSyntax)context.TargetNode;
        var symbol = (INamedTypeSymbol)context.TargetSymbol;

        if (!syntax.Modifiers.Any(static x => x.IsKind(SyntaxKind.PartialKeyword)))
        {
            return Results.Error<TypeModel>(new DiagnosticInfo(Diagnostics.InvalidTypeDefinition, syntax.GetLocation(), symbol.Name));
        }

        var ns = String.IsNullOrEmpty(symbol.ContainingNamespace.Name)
            ? string.Empty
            : symbol.ContainingNamespace.ToDisplayString();

        var containingTypes = default(List<ContainingTypeModel>?);
        var containingSymbol = symbol.ContainingType;
        while (containingSymbol != null)
        {
            containingTypes ??= [];
            containingTypes.Add(new ContainingTypeModel(containingSymbol.GetClassName(), containingSymbol.IsValueType));
            containingSymbol = containingSymbol.ContainingType;
        }
        containingTypes?.Reverse();

        var properties = new List<PropertyModel>();
        var currentSymbol = symbol;
        while (currentSymbol != null)
        {
            properties.AddRange(
                currentSymbol.GetMembers()
                    .OfType<IPropertySymbol>()
                    .Where(x => (x.DeclaredAccessibility == Accessibility.Public) &&
                                (x.GetMethod != null) &&
                                !x.IsWriteOnly &&
                                !x.GetAttributes().Any(attr => attr.AttributeClass?.ToDisplayString() == IgnoreAttributeName))
                    .Select(GetPropertyModel));
            currentSymbol = currentSymbol.BaseType;
        }

        return Results.Success(new TypeModel(
            ns,
            new EquatableArray<ContainingTypeModel>(containingTypes?.ToArray() ?? []),
            symbol.GetClassName(),
            symbol.IsValueType,
            new EquatableArray<PropertyModel>(properties.ToArray())));
    }

    private static PropertyModel GetPropertyModel(IPropertySymbol symbol)
    {
        var (hasElements, isNullAssignable) = GetPropertyType(symbol.Type);
        return new PropertyModel(
            symbol.Name,
            hasElements,
            isNullAssignable);
    }

    private static (bool HasElements, bool IsNullAssignable) GetPropertyType(ITypeSymbol typeSymbol)
    {
        if (!typeSymbol.SpecialType.Equals(SpecialType.System_String))
        {
            if (typeSymbol is IArrayTypeSymbol arrayTypeSymbol)
            {
                var elementType = arrayTypeSymbol.ElementType;
                return (true, elementType.IsReferenceType || elementType.IsGenericType());
            }

            foreach (var @interface in typeSymbol.AllInterfaces)
            {
                if (@interface.IsGenericType && (@interface.ConstructedFrom.ToDisplayString() == GenericEnumerableName))
                {
                    var elementType = @interface.TypeArguments[0];
                    return (true, elementType.IsReferenceType || elementType.IsGenericType());
                }
            }
        }

        return (false, typeSymbol.IsReferenceType || typeSymbol.IsGenericType());
    }
    // ------------------------------------------------------------
    // Builder
    // ------------------------------------------------------------

    private static void Execute(SourceProductionContext context, GeneratorOptions options, ImmutableArray<Result<TypeModel>> types)
    {
        foreach (var info in types.SelectError())
        {
            context.ReportDiagnostic(info);
        }

        var builder = new SourceBuilder();
        foreach (var type in types.SelectValue())
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            builder.Clear();
            BuildSource(builder, options, type);

            var filename = MakeFilename(type.Namespace, type.ContainingTypes, type.ClassName);
            var source = builder.ToString();
            context.AddSource(filename, SourceText.From(source, Encoding.UTF8));
        }
    }

    private static void BuildSource(SourceBuilder builder, GeneratorOptions options, TypeModel type)
    {
        var containingTypes = type.ContainingTypes.ToArray();

        builder.AutoGenerated();
        builder.EnableNullable();
        builder.NewLine();

        // namespace
        if (!String.IsNullOrEmpty(type.Namespace))
        {
            builder.Namespace(type.Namespace);
            builder.NewLine();
        }

        // containing types
        foreach (var containingType in containingTypes)
        {
            builder
                .Indent()
                .Append("partial ")
                .Append(containingType.IsValueType ? "struct " : "class ")
                .Append(containingType.ClassName)
                .NewLine();
            builder.BeginScope();
        }

        // class
        builder
            .Indent()
            .Append("partial ")
            .Append(type.IsValueType ? "struct " : "class ")
            .Append(type.ClassName)
            .NewLine();
        builder.BeginScope();

        // Method
        builder
            .Indent()
            .Append("public override string ToString()")
            .NewLine();
        builder.BeginScope();

        builder
            .Indent()
            .Append("var handler = new global::System.Runtime.CompilerServices.DefaultInterpolatedStringHandler(0, 0, default, stackalloc char[256]);")
            .NewLine();
        if (options.OutputClassName)
        {
            builder
                .Indent()
                .Append("handler.AppendLiteral(\"")
                .Append(type.ClassName)
                .Append(" \");")
                .NewLine();
        }
        builder
            .Indent()
            .Append("handler.AppendLiteral(\"{ \");")
            .NewLine();

        var firstProperty = true;
        foreach (var property in type.Properties.ToArray())
        {
            if (firstProperty)
            {
                firstProperty = false;
            }
            else
            {
                builder
                    .Indent()
                    .Append("handler.AppendLiteral(\", \");")
                    .NewLine();
            }

            builder.Indent()
                .Append("handler.AppendLiteral(\"")
                .Append(property.Name)
                .Append(" = \");")
                .NewLine();

            if (property.HasElements)
            {
                builder
                    .Indent()
                    .Append("if (this.")
                    .Append(property.Name)
                    .Append(" is not null)")
                    .NewLine();
                builder.BeginScope();

                BuildAppendLiteral(builder, "[");

                BuildAppendJoinedFormatted(builder, property.Name, property.IsNullAssignable, options.NullLiteral);

                BuildAppendLiteral(builder, "]");

                builder.EndScope();

                if (!String.IsNullOrEmpty(options.NullLiteral))
                {
                    builder
                        .Indent()
                        .Append("else")
                        .NewLine();
                    builder.BeginScope();

                    BuildAppendLiteral(builder, options.NullLiteral!);

                    builder.EndScope();
                }
            }
            else
            {
                if (property.IsNullAssignable)
                {
                    if (!String.IsNullOrEmpty(options.NullLiteral))
                    {
                        builder
                            .Indent()
                            .Append("if (this.")
                            .Append(property.Name)
                            .Append(" is not null)")
                            .NewLine();
                        builder.BeginScope();

                        BuildAppendProperty(builder, property.Name);

                        builder.EndScope();
                        builder
                            .Indent()
                            .Append("else")
                            .NewLine();
                        builder.BeginScope();

                        BuildAppendLiteral(builder, options.NullLiteral!);

                        builder.EndScope();
                    }
                    else
                    {
                        BuildAppendProperty(builder, property.Name);
                    }
                }
                else
                {
                    BuildAppendProperty(builder, property.Name);
                }
            }
        }

        builder
            .Indent()
            .Append("handler.AppendLiteral(\" }\");")
            .NewLine();
        builder
            .Indent()
            .Append("return handler.ToStringAndClear();")
            .NewLine();

        builder.EndScope();

        builder.EndScope();

        // end containing types
        for (var i = 0; i < containingTypes.Length; i++)
        {
            builder.EndScope();
        }
    }

    private static void BuildAppendProperty(SourceBuilder builder, string name)
    {
        builder
            .Indent()
            .Append("handler.AppendFormatted(")
            .Append("this.")
            .Append(name)
            .Append(");")
            .NewLine();
    }

    private static void BuildAppendLiteral(SourceBuilder builder, string literal)
    {
        builder
            .Indent()
            .Append("handler.AppendLiteral(\"")
            .Append(literal)
            .Append("\");")
            .NewLine();
    }

    private static void BuildAppendJoinedFormatted(SourceBuilder builder, string name, bool isNullAssignable, string? nullLiteral)
    {
        builder
            .Indent()
            .Append("var firstItem = true;")
            .NewLine();
        builder
            .Indent()
            .Append("foreach (var item in this.")
            .Append(name)
            .Append(")")
            .NewLine();
        builder.BeginScope();

        builder
            .Indent()
            .Append("if (firstItem) { firstItem = false; } else { handler.AppendLiteral(\", \"); }")
            .NewLine();

        if (isNullAssignable && !String.IsNullOrEmpty(nullLiteral))
        {
            builder
                .Indent()
                .Append("if (item is not null) { handler.AppendFormatted(item); } else { handler.AppendLiteral(\"")
                .Append(nullLiteral!)
                .Append("\"); }")
                .NewLine();
        }
        else
        {
            builder
                .Indent()
                .Append("handler.AppendFormatted(item);")
                .NewLine();
        }

        builder.EndScope();
    }

    // ------------------------------------------------------------
    // Helper
    // ------------------------------------------------------------

    private static string MakeFilename(string ns, EquatableArray<ContainingTypeModel> containingTypes, string className)
    {
        var buffer = new StringBuilder();

        if (!String.IsNullOrEmpty(ns))
        {
            buffer.Append(ns.Replace('.', '_'));
            buffer.Append('_');
        }

        foreach (var containingType in containingTypes.ToArray())
        {
            buffer.Append(containingType.ClassName.Replace('<', '[').Replace('>', ']'));
            buffer.Append('_');
        }

        buffer.Append(className.Replace('<', '[').Replace('>', ']'));
        buffer.Append(".g.cs");

        return buffer.ToString();
    }
}
