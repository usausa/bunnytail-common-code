namespace BunnyTail.CommonCode.Generator;

using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;

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
    private const string FormatAttributeName = "BunnyTail.CommonCode.ToStringFormatAttribute";
    private const string MaskAttributeName = "BunnyTail.CommonCode.ToStringMaskAttribute";
    private const string MaxLengthAttributeName = "BunnyTail.CommonCode.ToStringMaxLengthAttribute";

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

        context.RegisterSourceOutput(
            targetProvider,
            static (spc, types) => ReportDiagnostics(spc, types));

        var models = targetProvider
            .SelectMany(static (types, _) => types.SelectValue().ToImmutableArray())
            .Combine(optionProvider);
        context.RegisterImplementationSourceOutput(
            models,
            static (spc, pair) => Execute(spc, pair.Right, pair.Left));
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
            return Results.Error<TypeModel>(new DiagnosticInfo(Diagnostics.InvalidTypeDefinition, syntax.Identifier.GetLocation(), symbol.Name));
        }

        var ns = String.IsNullOrEmpty(symbol.ContainingNamespace.Name)
            ? string.Empty
            : symbol.ContainingNamespace.ToDisplayString();

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
        var seenNames = new HashSet<string>(StringComparer.Ordinal);
        var currentSymbol = symbol;
        while (currentSymbol is not null)
        {
            foreach (var member in currentSymbol.GetMembers().OfType<IPropertySymbol>())
            {
                // インデクサは this.<Name> でアクセスできないため対象外
                // Indexers are excluded because they cannot be accessed via this.<Name>
                if (member.IsIndexer)
                {
                    continue;
                }

                // this.<Name> は最派生の宣言に束縛されるため、隠蔽された基底側の同名プロパティは収集しない。
                // 可視性 / IgnoreToString 判定より前で登録するのは意図的: 派生の private / ignore な new 隠蔽でも、
                // this.<Name> から到達できない基底 public を誤って拾わず、隠蔽 / ignore したメンバの値を出力しない。
                // Since this.<Name> binds to the most-derived declaration, a hidden base property of the same name is not collected.
                // Registering before the visibility / IgnoreToString check is intentional: even for a derived private / ignored new-hiding member,
                // this avoids wrongly picking up a base public unreachable from this.<Name>, and avoids outputting the value of a hidden / ignored member.
                if (!seenNames.Add(member.Name))
                {
                    continue;
                }

                if (member.DeclaredAccessibility != Accessibility.Public)
                {
                    continue;
                }

                if (member.GetMethod is null)
                {
                    continue;
                }

                if (member.IsWriteOnly)
                {
                    continue;
                }

                if (member.GetAttributes().Any(attr => attr.AttributeClass?.ToDisplayString() == IgnoreAttributeName))
                {
                    continue;
                }

                properties.Add(GetPropertyModel(member));
            }
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

        string? format = null;
        int? maxLength = null;
        var mask = false;
        var maskShow = 0;
        foreach (var attr in symbol.GetAttributes())
        {
            switch (attr.AttributeClass?.ToDisplayString())
            {
                case FormatAttributeName:
                    if ((attr.ConstructorArguments.Length > 0) && (attr.ConstructorArguments[0].Value is string formatValue))
                    {
                        format = formatValue;
                    }
                    break;
                case MaxLengthAttributeName:
                    if ((attr.ConstructorArguments.Length > 0) && (attr.ConstructorArguments[0].Value is int maxLengthValue))
                    {
                        maxLength = maxLengthValue;
                    }
                    break;
                case MaskAttributeName:
                    mask = true;
                    var showArg = attr.NamedArguments.FirstOrDefault(static na => na.Key == "Show");
                    if (!showArg.Value.IsNull && (showArg.Value.Value is int showValue))
                    {
                        maskShow = showValue;
                    }
                    break;
            }
        }

        return new PropertyModel(
            symbol.Name,
            hasElements,
            isNullAssignable,
            format,
            maxLength,
            mask,
            maskShow);
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

            if (typeSymbol is INamedTypeSymbol { IsGenericType: true } namedTypeSymbol &&
                (namedTypeSymbol.ConstructedFrom.ToDisplayString() == GenericEnumerableName))
            {
                var elementType = namedTypeSymbol.TypeArguments[0];
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

    private static void ReportDiagnostics(SourceProductionContext context, ImmutableArray<Result<TypeModel>> types)
    {
        foreach (var info in types.SelectError())
        {
            context.ReportDiagnostic(info);
        }
    }

    private static void Execute(SourceProductionContext context, GeneratorOptions options, TypeModel type)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        var builder = new SourceBuilder();
        BuildSource(builder, options, type);

        var filename = MakeFilename(type.Namespace, type.ContainingTypes, type.ClassName);
        var source = builder.ToString();
        context.AddSource(filename, SourceText.From(source, Encoding.UTF8));
    }

    private static void BuildSource(SourceBuilder builder, GeneratorOptions options, TypeModel type)
    {
        var containingTypes = type.ContainingTypes;

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
        foreach (var property in type.Properties)
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
            else if (property.Mask || property.MaxLength.HasValue)
            {
                BuildAppendMasked(builder, property, options.NullLiteral);
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

                        BuildAppendProperty(builder, property.Name, property.Format);

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
                        BuildAppendProperty(builder, property.Name, property.Format);
                    }
                }
                else
                {
                    BuildAppendProperty(builder, property.Name, property.Format);
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
        for (var i = 0; i < containingTypes.Count; i++)
        {
            builder.EndScope();
        }
    }

    private static void BuildAppendProperty(SourceBuilder builder, string name, string? format)
    {
        builder
            .Indent()
            .Append("handler.AppendFormatted(")
            .Append("this.")
            .Append(name);
        if (!String.IsNullOrEmpty(format))
        {
            builder
                .Append(", \"")
                .Append(EscapeString(format!))
                .Append("\"");
        }
        builder
            .Append(");")
            .NewLine();
    }

    private static void BuildAppendMasked(SourceBuilder builder, PropertyModel property, string? nullLiteral)
    {
        builder.BeginScope();

        builder.Indent().Append("var value = ");
        if (!property.Mask && !String.IsNullOrEmpty(property.Format))
        {
            builder
                .Append("this.")
                .Append(property.Name)
                .Append(" is global::System.IFormattable formattable ? formattable.ToString(\"")
                .Append(EscapeString(property.Format!))
                .Append("\", null) : this.")
                .Append(property.Name)
                .Append(property.IsNullAssignable ? "?.ToString();" : ".ToString();")
                .NewLine();
        }
        else
        {
            builder
                .Append("this.")
                .Append(property.Name)
                .Append(property.IsNullAssignable ? "?.ToString();" : ".ToString();")
                .NewLine();
        }

        builder.Indent().Append("if (value is null)").NewLine();
        builder.BeginScope();
        if (!String.IsNullOrEmpty(nullLiteral))
        {
            BuildAppendLiteral(builder, nullLiteral!);
        }
        builder.EndScope();
        builder.Indent().Append("else").NewLine();
        builder.BeginScope();

        if (property.Mask)
        {
            if (property.MaskShow > 0)
            {
                builder
                    .Indent()
                    .Append("if (value.Length > ")
                    .Append(property.MaskShow.ToString(CultureInfo.InvariantCulture))
                    .Append(")")
                    .NewLine();
                builder.BeginScope();
                BuildAppendLiteral(builder, "***");
                builder
                    .Indent()
                    .Append("handler.AppendFormatted(value.Substring(value.Length - ")
                    .Append(property.MaskShow.ToString(CultureInfo.InvariantCulture))
                    .Append("));")
                    .NewLine();
                builder.EndScope();
                builder.Indent().Append("else").NewLine();
                builder.BeginScope();
                BuildAppendLiteral(builder, "***");
                builder.EndScope();
            }
            else
            {
                BuildAppendLiteral(builder, "***");
            }
        }
        else
        {
            builder
                .Indent()
                .Append("if (value.Length > ")
                .Append(property.MaxLength!.Value.ToString(CultureInfo.InvariantCulture))
                .Append(")")
                .NewLine();
            builder.BeginScope();
            builder
                .Indent()
                .Append("handler.AppendFormatted(value.Substring(0, ")
                .Append(property.MaxLength!.Value.ToString(CultureInfo.InvariantCulture))
                .Append("));")
                .NewLine();
            builder.EndScope();
            builder.Indent().Append("else").NewLine();
            builder.BeginScope();
            builder.Indent().Append("handler.AppendFormatted(value);").NewLine();
            builder.EndScope();
        }

        builder.EndScope();

        builder.EndScope();
    }

    private static string EscapeString(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"");

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

        foreach (var containingType in containingTypes)
        {
            buffer.Append(containingType.ClassName.Replace('<', '[').Replace('>', ']'));
            buffer.Append('_');
        }

        buffer.Append(className.Replace('<', '[').Replace('>', ']'));
        buffer.Append(".g.cs");

        return buffer.ToString();
    }

    // ------------------------------------------------------------
    // Models
    // ------------------------------------------------------------

    private sealed record GeneratorOptions
    {
        public bool OutputClassName { get; set; }

        public string? NullLiteral { get; set; } = "null";
    }

    private sealed record ContainingTypeModel(
        string ClassName,
        bool IsValueType);

    private sealed record TypeModel(
        string Namespace,
        EquatableArray<ContainingTypeModel> ContainingTypes,
        string ClassName,
        bool IsValueType,
        EquatableArray<PropertyModel> Properties);

    private sealed record PropertyModel(
        string Name,
        bool HasElements,
        bool IsNullAssignable,
        string? Format,
        int? MaxLength,
        bool Mask,
        int MaskShow);
}
