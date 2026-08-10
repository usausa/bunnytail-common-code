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

    private const string GenericEnumerableName = "System.Collections.Generic.IEnumerable<T>";

    private const string OptionPrefix = "CommonCodeGeneratorToString";

    private const string EllipsisLiteral = "...";

    private const char MaskKeepChar = '#';

    private const string TypeNameCacheField = "GeneratedToStringPrefix";
    private const string TypeNameFormatMethod = "GeneratedToStringFormatTypeName";

    private static readonly string[] PrimitiveTypeNames =
    [
        "bool",
        "byte",
        "sbyte",
        "char",
        "decimal",
        "double",
        "float",
        "int",
        "uint",
        "long",
        "ulong",
        "short",
        "ushort",
        "object",
        "string"
    ];

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
            .SelectMany(static (x, _) => x is not null ? ImmutableArray.Create(x) : []);

        context.RegisterSourceOutput(
            targetProvider,
            static (spc, result) => ReportDiagnostics(spc, result));

        var models = targetProvider
            .Where(static x => x.HasValue)
            .Select(static (x, _) => x.Value)
            .WithTrackingName("Models")
            .Combine(optionProvider);
        context.RegisterImplementationSourceOutput(
            models,
            static (spc, pair) => Execute(spc, pair.Right, pair.Left));
    }

    private static bool IsTypeSyntax(SyntaxNode node) =>
        node is ClassDeclarationSyntax or StructDeclarationSyntax;

    // ------------------------------------------------------------
    // Option
    // ------------------------------------------------------------

    private static OptionModel GetOptions(AnalyzerConfigOptionsProvider provider)
    {
        var options = provider.GlobalOptions;

        var typeName = GetEnumOption(options, "TypeName", TypeNameOption.Simple);
        var typeArgument = GetEnumOption(options, "TypeArgument", TypeArgumentOption.Include);
        var nullMode = GetEnumOption(options, "Null", NullOption.Literal);
        var nullLiteral = GetStringOption(options, "NullLiteral", "null");
        var collection = GetEnumOption(options, "Collection", CollectionOption.Expand);
        var collectionLimit = GetIntOption(options, "CollectionLimit", -1);
        var members = GetEnumOption(options, "Members", MemberKindOption.Property);
        var innerSpace = GetEnumOption(options, "InnerSpace", SpaceOption.Space);
        var typeNameSpace = GetEnumOption(options, "TypeNameSpace", SpaceOption.Space);
        var separator = GetStringOption(options, "Separator", ", ");
        var assign = GetStringOption(options, "Assign", " = ");
        var collectionInnerSpace = GetEnumOption(options, "CollectionInnerSpace", SpaceOption.None);
        var collectionSeparator = GetStringOption(options, "CollectionSeparator", ", ");

        var bracket = GetEnumOption(options, "Bracket", BracketOption.Brace);
        var openBracket = ResolveBracket(bracket, GetStringOption(options, "OpenBracket", string.Empty), true);
        var closeBracket = ResolveBracket(bracket, GetStringOption(options, "CloseBracket", string.Empty), false);
        var hasBracket = (openBracket.Length > 0) || (closeBracket.Length > 0);

        var collectionBracket = GetEnumOption(options, "CollectionBracket", BracketOption.Square);
        var collectionOpenBracket = ResolveBracket(collectionBracket, GetStringOption(options, "CollectionOpenBracket", string.Empty), true);
        var collectionCloseBracket = ResolveBracket(collectionBracket, GetStringOption(options, "CollectionCloseBracket", string.Empty), false);
        var hasCollectionBracket = (collectionOpenBracket.Length > 0) || (collectionCloseBracket.Length > 0);

        return new OptionModel(
            typeName,
            typeArgument,
            nullMode,
            nullLiteral,
            collection,
            collectionLimit,
            members,
            openBracket,
            closeBracket,
            hasBracket ? ResolveSpace(innerSpace) : string.Empty,
            typeName != TypeNameOption.None ? ResolveSpace(typeNameSpace) : string.Empty,
            separator,
            assign,
            collectionOpenBracket,
            collectionCloseBracket,
            hasCollectionBracket ? ResolveSpace(collectionInnerSpace) : string.Empty,
            collectionSeparator);
    }

    private static string ResolveBracket(BracketOption bracket, string value, bool open)
    {
        if (value.Length > 0)
        {
            return value;
        }

        return bracket switch
        {
            BracketOption.Brace => open ? "{" : "}",
            BracketOption.Parenthesis => open ? "(" : ")",
            BracketOption.Square => open ? "[" : "]",
            BracketOption.Angle => open ? "<" : ">",
            _ => string.Empty
        };
    }

    private static string ResolveSpace(SpaceOption space) =>
        space == SpaceOption.Space ? " " : string.Empty;

    private static T GetEnumOption<T>(AnalyzerConfigOptions options, string name, T defaultValue)
        where T : struct, Enum
    {
        var value = options.GetValue<string?>(OptionPrefix + name);
        return !String.IsNullOrEmpty(value) && Enum.TryParse<T>(value, true, out var result) ? result : defaultValue;
    }

    private static string GetStringOption(AnalyzerConfigOptions options, string name, string defaultValue)
    {
        var value = Unquote(options.GetValue<string?>(OptionPrefix + name));
        return String.IsNullOrEmpty(value) ? defaultValue : value!;
    }

    private static int GetIntOption(AnalyzerConfigOptions options, string name, int defaultValue)
    {
        var value = options.GetValue<string?>(OptionPrefix + name);
        return !String.IsNullOrEmpty(value) && Int32.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) && (result != 0)
            ? result
            : defaultValue;
    }

    // MSBuild trims the surrounding whitespace of a property value, so quoting allows a value that contains it.
    private static string? Unquote(string? value) =>
        (value is not null) && (value.Length >= 2) && (value[0] == '"') && (value[value.Length - 1] == '"')
            ? value.Substring(1, value.Length - 2)
            : value;

    // ------------------------------------------------------------
    // Model
    // ------------------------------------------------------------

    private static Result<TypeModel> GetTypeModel(GeneratorAttributeSyntaxContext context)
    {
        var syntax = (TypeDeclarationSyntax)context.TargetNode;
        var symbol = (INamedTypeSymbol)context.TargetSymbol;

        if (!syntax.Modifiers.Any(static x => x.IsKind(SyntaxKind.PartialKeyword)))
        {
            return Results.Error<TypeModel>(new DiagnosticInfo(Diagnostics.InvalidTypeDefinition, syntax.Identifier.GetLocation(), symbol.Name));
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

        var diagnostics = new List<DiagnosticInfo>();
        var members = CollectMembers(symbol, diagnostics);

        return Results.Success(new TypeModel(
            ns,
            new EquatableArray<ContainingTypeModel>(containingTypes?.ToArray() ?? []),
            symbol.GetClassName(),
            symbol.IsValueType,
            symbol.Name,
            MakeFullName(symbol, ns),
            new EquatableArray<string>(symbol.TypeParameters.Select(static x => x.Name).ToArray()),
            new EquatableArray<MemberModel>(members.ToArray()),
            new EquatableArray<DiagnosticInfo>(diagnostics.ToArray())));
    }

    private static List<MemberModel> CollectMembers(INamedTypeSymbol symbol, List<DiagnosticInfo> diagnostics)
    {
        var levels = new List<List<MemberModel>>();
        var seenNames = new HashSet<string>(StringComparer.Ordinal);
        var currentSymbol = symbol;
        while (currentSymbol is not null)
        {
            var members = new List<MemberModel>();
            foreach (var member in currentSymbol.GetMembers())
            {
                // Static members are excluded
                if (member.IsStatic)
                {
                    continue;
                }

                if (member is IPropertySymbol property)
                {
                    // Indexers are excluded
                    if (property.IsIndexer)
                    {
                        continue;
                    }

                    // Skip duplicate property names (hides base property)
                    if (!seenNames.Add(property.Name))
                    {
                        continue;
                    }

                    if (HasIgnore(property))
                    {
                        ReportFormatOnIgnored(diagnostics, property);
                        continue;
                    }

                    if ((property.DeclaredAccessibility != Accessibility.Public) ||
                        (property.GetMethod is null) ||
                        property.IsWriteOnly)
                    {
                        continue;
                    }

                    members.Add(GetMemberModel(property, property.Type, false, diagnostics));
                }
                else if (member is IFieldSymbol field)
                {
                    // Compiler generated fields are excluded
                    if (field.IsImplicitlyDeclared || (field.AssociatedSymbol is not null))
                    {
                        continue;
                    }

                    // Skip duplicate field names (hides base field)
                    if (!seenNames.Add(field.Name))
                    {
                        continue;
                    }

                    if (HasIgnore(field))
                    {
                        ReportFormatOnIgnored(diagnostics, field);
                        continue;
                    }

                    if (field.DeclaredAccessibility != Accessibility.Public)
                    {
                        continue;
                    }

                    members.Add(GetMemberModel(field, field.Type, true, diagnostics));
                }
            }

            levels.Add(members);
            currentSymbol = currentSymbol.BaseType;
        }

        // Base type members are output first, same as record
        levels.Reverse();

        var result = new List<MemberModel>();
        foreach (var level in levels)
        {
            result.AddRange(level);
        }

        return result;
    }

    private static bool HasIgnore(ISymbol symbol) =>
        symbol.GetAttributes().Any(static x => x.AttributeClass?.ToDisplayString() == IgnoreAttributeName);

    private static AttributeData? GetFormatAttribute(ISymbol symbol) =>
        symbol.GetAttributes().FirstOrDefault(static x => x.AttributeClass?.ToDisplayString() == FormatAttributeName);

    private static Location? GetSourceLocation(ISymbol symbol) =>
        symbol.Locations.FirstOrDefault(static x => x.IsInSource);

    private static void ReportFormatOnIgnored(List<DiagnosticInfo> diagnostics, ISymbol symbol)
    {
        if (GetFormatAttribute(symbol) is null)
        {
            return;
        }

        var location = GetSourceLocation(symbol);
        if (location is not null)
        {
            diagnostics.Add(new DiagnosticInfo(Diagnostics.ToStringFormatOnIgnored, location, symbol.Name));
        }
    }

    private static MemberModel GetMemberModel(ISymbol symbol, ITypeSymbol type, bool isField, List<DiagnosticInfo> diagnostics)
    {
        var (hasElements, isNullAssignable, isElementNullAssignable) = GetMemberType(type);

        var format = default(string?);
        var maskPattern = default(string?);
        var maskChar = '\0';
        var maxLength = 0;

        var attr = GetFormatAttribute(symbol);
        if (attr is not null)
        {
            if ((attr.ConstructorArguments.Length > 0) && (attr.ConstructorArguments[0].Value is string formatValue))
            {
                format = formatValue;
            }

            foreach (var argument in attr.NamedArguments)
            {
                switch (argument.Key)
                {
                    case "MaxLength":
                        if (argument.Value.Value is int maxLengthValue)
                        {
                            maxLength = maxLengthValue;
                        }
                        break;
                    case "MaskChar":
                        if (argument.Value.Value is char maskCharValue)
                        {
                            maskChar = maskCharValue;
                        }
                        break;
                    case "MaskPattern":
                        if (argument.Value.Value is string maskPatternValue)
                        {
                            maskPattern = maskPatternValue;
                        }
                        break;
                }
            }

            var location = GetSourceLocation(symbol);
            if (location is not null)
            {
                if ((maskChar != '\0') && !String.IsNullOrEmpty(maskPattern))
                {
                    diagnostics.Add(new DiagnosticInfo(Diagnostics.ToStringMaskConflict, location, symbol.Name));
                }
                else if (String.IsNullOrEmpty(format) && (maxLength <= 0) && (maskChar == '\0') && String.IsNullOrEmpty(maskPattern))
                {
                    diagnostics.Add(new DiagnosticInfo(Diagnostics.ToStringFormatNoEffect, location, symbol.Name));
                }
            }
        }

        return new MemberModel(
            symbol.Name,
            isField,
            hasElements,
            isNullAssignable,
            isElementNullAssignable,
            format,
            maxLength,
            maskChar,
            maskPattern);
    }

    private static (bool HasElements, bool IsNullAssignable, bool IsElementNullAssignable) GetMemberType(ITypeSymbol typeSymbol)
    {
        var isNullAssignable = typeSymbol.IsReferenceType || typeSymbol.IsGenericType();

        if (!typeSymbol.SpecialType.Equals(SpecialType.System_String))
        {
            if (typeSymbol is IArrayTypeSymbol arrayTypeSymbol)
            {
                var elementType = arrayTypeSymbol.ElementType;
                return (true, isNullAssignable, elementType.IsReferenceType || elementType.IsGenericType());
            }

            if (typeSymbol is INamedTypeSymbol { IsGenericType: true } namedTypeSymbol &&
                (namedTypeSymbol.ConstructedFrom.ToDisplayString() == GenericEnumerableName))
            {
                var elementType = namedTypeSymbol.TypeArguments[0];
                return (true, isNullAssignable, elementType.IsReferenceType || elementType.IsGenericType());
            }

            foreach (var iface in typeSymbol.AllInterfaces)
            {
                if (iface.IsGenericType && (iface.ConstructedFrom.ToDisplayString() == GenericEnumerableName))
                {
                    var elementType = iface.TypeArguments[0];
                    return (true, isNullAssignable, elementType.IsReferenceType || elementType.IsGenericType());
                }
            }
        }

        return (false, isNullAssignable, false);
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

    private static void Execute(SourceProductionContext context, OptionModel options, TypeModel type)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        var builder = new SourceBuilder();
        BuildSource(builder, options, type);

        var filename = MakeFilename(type.Namespace, type.ContainingTypes, type.ClassName);
        var source = builder.ToString();
        context.AddSource(filename, SourceText.From(source, Encoding.UTF8));
    }

    private static void BuildSource(SourceBuilder builder, OptionModel options, TypeModel type)
    {
        var containingTypes = type.ContainingTypes;

        builder.AutoGenerated();
        builder.EnableNullable();
        builder.NewLine();

        // Namespace
        if (!String.IsNullOrEmpty(type.Namespace))
        {
            builder.Namespace(type.Namespace);
            builder.NewLine();
        }

        // Containing types
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

        // Class
        builder
            .Indent()
            .Append("partial ")
            .Append(type.IsValueType ? "struct " : "class ")
            .Append(type.ClassName)
            .NewLine();
        builder.BeginScope();

        // Method
        BuildMember(builder, options, type);

        builder.EndScope();

        // End containing types
        for (var i = 0; i < containingTypes.Count; i++)
        {
            builder.EndScope();
        }
    }

    private static void BuildMember(SourceBuilder builder, OptionModel options, TypeModel type)
    {
        var members = new List<MemberModel>();
        // ReSharper disable once LoopCanBeConvertedToQuery
        foreach (var member in type.Members)
        {
            if (member.IsField && (options.Members != MemberKindOption.PropertyAndField))
            {
                continue;
            }

            members.Add(member);
        }

        var typeName = options.TypeName switch
        {
            TypeNameOption.Simple => type.SimpleName,
            TypeNameOption.Full => type.FullName,
            _ => string.Empty
        };
        var useTypeArgument = (options.TypeArgument == TypeArgumentOption.Include) &&
                              (options.TypeName != TypeNameOption.None) &&
                              (type.TypeParameters.Count > 0);

        var head = new StringBuilder();
        head.Append(options.TypeNameSpace).Append(options.OpenBracket).Append(options.InnerSpace);
        if (members.Count == 0)
        {
            head.Append(options.CloseBracket);
        }
        else
        {
            head.Append(members[0].Name).Append(options.Assign);
        }

        if (useTypeArgument)
        {
            BuildTypeNameCache(builder, type, typeName, head.ToString());
            builder.NewLine();
        }

        builder
            .Indent()
            .Append("public override string ToString()")
            .NewLine();
        builder.BeginScope();

        if (members.Count == 0)
        {
            builder.Indent().Append("return ");
            if (useTypeArgument)
            {
                builder.Append(TypeNameCacheField).Append(";").NewLine();
            }
            else
            {
                builder.Append("\"").Append(EscapeString(typeName + head)).Append("\";").NewLine();
            }

            builder.EndScope();
            return;
        }

        builder
            .Indent()
            .Append("var handler = new global::System.Runtime.CompilerServices.DefaultInterpolatedStringHandler(0, 0, default, stackalloc char[256]);")
            .NewLine();

        if (useTypeArgument)
        {
            builder.Indent().Append("handler.AppendLiteral(").Append(TypeNameCacheField).Append(");").NewLine();
        }
        else
        {
            BuildAppendLiteral(builder, typeName + head);
        }

        BuildMemberValue(builder, options, members[0]);

        var pending = new StringBuilder();
        for (var i = 1; i < members.Count; i++)
        {
            var member = members[i];
            pending.Append(options.Separator).Append(member.Name).Append(options.Assign);

            FlushLiteral(builder, pending);

            BuildMemberValue(builder, options, member);
        }

        pending.Append(options.InnerSpace).Append(options.CloseBracket);
        FlushLiteral(builder, pending);

        builder
            .Indent()
            .Append("return handler.ToStringAndClear();")
            .NewLine();

        builder.EndScope();
    }

    private static void BuildTypeNameCache(SourceBuilder builder, TypeModel type, string typeName, string head)
    {
        builder
            .Indent()
            .Append("private static readonly string ")
            .Append(TypeNameCacheField)
            .Append(" = \"")
            .Append(EscapeString(typeName))
            .Append("<\"");
        var firstParameter = true;
        foreach (var typeParameter in type.TypeParameters)
        {
            if (firstParameter)
            {
                firstParameter = false;
            }
            else
            {
                builder.Append(" + \", \"");
            }

            builder
                .Append(" + ")
                .Append(TypeNameFormatMethod)
                .Append("(typeof(")
                .Append(typeParameter)
                .Append("))");
        }
        builder
            .Append(" + \">")
            .Append(EscapeString(head))
            .Append("\";")
            .NewLine();
        builder.NewLine();

        builder
            .Indent()
            .Append("private static string ")
            .Append(TypeNameFormatMethod)
            .Append("(global::System.Type type)")
            .NewLine();
        builder.BeginScope();

        builder.Indent().Append("if (type.IsArray)").NewLine();
        builder.BeginScope();
        builder
            .Indent()
            .Append("return ")
            .Append(TypeNameFormatMethod)
            .Append("(type.GetElementType()!) + \"[\" + new string(',', type.GetArrayRank() - 1) + \"]\";")
            .NewLine();
        builder.EndScope();

        builder.Indent().Append("var underlying = global::System.Nullable.GetUnderlyingType(type);").NewLine();
        builder.Indent().Append("if (underlying is not null)").NewLine();
        builder.BeginScope();
        builder.Indent().Append("return ").Append(TypeNameFormatMethod).Append("(underlying) + \"?\";").NewLine();
        builder.EndScope();

        foreach (var keyword in PrimitiveTypeNames)
        {
            builder
                .Indent()
                .Append("if (type == typeof(")
                .Append(keyword)
                .Append(")) { return \"")
                .Append(keyword)
                .Append("\"; }")
                .NewLine();
        }

        builder.Indent().Append("if (!type.IsGenericType)").NewLine();
        builder.BeginScope();
        builder.Indent().Append("return type.Name;").NewLine();
        builder.EndScope();

        builder.Indent().Append("var name = type.Name;").NewLine();
        builder.Indent().Append("var index = name.IndexOf('`');").NewLine();
        builder.Indent().Append("if (index >= 0) { name = name.Substring(0, index); }").NewLine();
        builder.Indent().Append("var arguments = type.GetGenericArguments();").NewLine();
        builder.Indent().Append("var buffer = new global::System.Text.StringBuilder(name);").NewLine();
        builder.Indent().Append("buffer.Append('<');").NewLine();
        builder.Indent().Append("for (var i = 0; i < arguments.Length; i++)").NewLine();
        builder.BeginScope();
        builder.Indent().Append("if (i > 0) { buffer.Append(\", \"); }").NewLine();
        builder.Indent().Append("buffer.Append(").Append(TypeNameFormatMethod).Append("(arguments[i]));").NewLine();
        builder.EndScope();
        builder.Indent().Append("buffer.Append('>');").NewLine();
        builder.Indent().Append("return buffer.ToString();").NewLine();

        builder.EndScope();
    }

    private static void BuildMemberValue(SourceBuilder builder, OptionModel options, MemberModel member)
    {
        if (!String.IsNullOrEmpty(member.MaskPattern))
        {
            BuildAppendMaskPattern(builder, options, member);
        }
        else if (member.MaskChar != '\0')
        {
            BuildAppendMaskChar(builder, options, member);
        }
        else if (member.HasElements && (options.Collection == CollectionOption.Expand))
        {
            BuildAppendCollection(builder, options, member);
        }
        else if (member.MaxLength > 0)
        {
            BuildAppendMaxLength(builder, options, member);
        }
        else if (member.IsNullAssignable && (options.Null == NullOption.Literal))
        {
            builder
                .Indent()
                .Append("if (this.")
                .Append(member.Name)
                .Append(" is not null)")
                .NewLine();
            builder.BeginScope();

            BuildAppendFormatted(builder, member);

            builder.EndScope();
            builder
                .Indent()
                .Append("else")
                .NewLine();
            builder.BeginScope();

            BuildAppendLiteral(builder, options.NullLiteral);

            builder.EndScope();
        }
        else
        {
            BuildAppendFormatted(builder, member);
        }
    }

    private static void BuildAppendCollection(SourceBuilder builder, OptionModel options, MemberModel member)
    {
        if (member.IsNullAssignable)
        {
            builder
                .Indent()
                .Append("if (this.")
                .Append(member.Name)
                .Append(" is not null)")
                .NewLine();
            builder.BeginScope();

            BuildCollectionBody(builder, options, member);

            builder.EndScope();

            if (options.Null == NullOption.Literal)
            {
                builder
                    .Indent()
                    .Append("else")
                    .NewLine();
                builder.BeginScope();

                BuildAppendLiteral(builder, options.NullLiteral);

                builder.EndScope();
            }
        }
        else
        {
            builder.BeginScope();

            BuildCollectionBody(builder, options, member);

            builder.EndScope();
        }
    }

    private static void BuildCollectionBody(SourceBuilder builder, OptionModel options, MemberModel member)
    {
        var limited = options.CollectionLimit > 0;
        var hasInnerSpace = options.CollectionInnerSpace.Length > 0;

        BuildAppendLiteral(builder, options.CollectionOpenBracket);

        builder
            .Indent()
            .Append(limited || hasInnerSpace ? "var itemIndex = 0;" : "var firstItem = true;")
            .NewLine();
        builder
            .Indent()
            .Append("foreach (var item in this.")
            .Append(member.Name)
            .Append(")")
            .NewLine();
        builder.BeginScope();

        if (limited || hasInnerSpace)
        {
            builder.Indent().Append("if (itemIndex > 0) { handler.AppendLiteral(\"").Append(EscapeString(options.CollectionSeparator)).Append("\"); }");
            if (hasInnerSpace)
            {
                builder.Append(" else { handler.AppendLiteral(\"").Append(EscapeString(options.CollectionInnerSpace)).Append("\"); }");
            }
            builder.NewLine();

            if (limited)
            {
                builder
                    .Indent()
                    .Append("if (itemIndex == ")
                    .Append(options.CollectionLimit.ToString(CultureInfo.InvariantCulture))
                    .Append(") { handler.AppendLiteral(\"")
                    .Append(EllipsisLiteral)
                    .Append("\"); break; }")
                    .NewLine();
            }

            builder.Indent().Append("itemIndex++;").NewLine();
        }
        else
        {
            builder
                .Indent()
                .Append("if (firstItem) { firstItem = false; } else { handler.AppendLiteral(\"")
                .Append(EscapeString(options.CollectionSeparator))
                .Append("\"); }")
                .NewLine();
        }

        if (member.IsElementNullAssignable && (options.Null == NullOption.Literal))
        {
            builder
                .Indent()
                .Append("if (item is not null) { handler.AppendFormatted(item); } else { handler.AppendLiteral(\"")
                .Append(EscapeString(options.NullLiteral))
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

        if (hasInnerSpace)
        {
            builder
                .Indent()
                .Append("if (itemIndex > 0) { handler.AppendLiteral(\"")
                .Append(EscapeString(options.CollectionInnerSpace))
                .Append("\"); }")
                .NewLine();
        }

        BuildAppendLiteral(builder, options.CollectionCloseBracket);
    }

    private static void BuildAppendFormatted(SourceBuilder builder, MemberModel member)
    {
        builder
            .Indent()
            .Append("handler.AppendFormatted(")
            .Append("this.")
            .Append(member.Name);
        if (!String.IsNullOrEmpty(member.Format))
        {
            builder
                .Append(", \"")
                .Append(EscapeString(member.Format!))
                .Append("\"");
        }
        builder
            .Append(");")
            .NewLine();
    }

    private static void BuildAppendMaskChar(SourceBuilder builder, OptionModel options, MemberModel member)
    {
        builder.BeginScope();

        BuildValueLocal(builder, member);
        BuildNullBranch(builder, options);

        builder
            .Indent()
            .Append("handler.AppendFormatted(new string('")
            .Append(EscapeChar(member.MaskChar))
            .Append("', ");
        if (member.MaxLength > 0)
        {
            builder
                .Append("global::System.Math.Min(value.Length, ")
                .Append(member.MaxLength.ToString(CultureInfo.InvariantCulture))
                .Append(")");
        }
        else
        {
            builder.Append("value.Length");
        }
        builder
            .Append("));")
            .NewLine();

        builder.EndScope();

        builder.EndScope();
    }

    private static void BuildAppendMaskPattern(SourceBuilder builder, OptionModel options, MemberModel member)
    {
        var (head, text, tail) = ParseMask(member.MaskPattern!);
        var keep = head + tail;

        // MaxLength is applied after masking
        var headTake = head;
        var maskText = text;
        var tailTake = tail;
        var shortText = text;
        if (member.MaxLength > 0)
        {
            headTake = Math.Min(head, member.MaxLength);
            maskText = Truncate(text, member.MaxLength - headTake);
            tailTake = Math.Min(tail, member.MaxLength - headTake - maskText.Length);
            shortText = Truncate(text, member.MaxLength);
        }

        if (keep == 0)
        {
            if (!member.IsNullAssignable)
            {
                BuildAppendLiteral(builder, shortText);
                return;
            }

            builder.Indent().Append("if (this.").Append(member.Name).Append(" is not null)").NewLine();
            builder.BeginScope();
            BuildAppendLiteral(builder, shortText);
            builder.EndScope();

            if (options.Null == NullOption.Literal)
            {
                builder.Indent().Append("else").NewLine();
                builder.BeginScope();
                BuildAppendLiteral(builder, options.NullLiteral);
                builder.EndScope();
            }

            return;
        }

        builder.BeginScope();

        BuildValueLocal(builder, member);
        BuildNullBranch(builder, options);

        builder
            .Indent()
            .Append("if (value.Length > ")
            .Append(keep.ToString(CultureInfo.InvariantCulture))
            .Append(")")
            .NewLine();
        builder.BeginScope();
        if (headTake > 0)
        {
            builder
                .Indent()
                .Append("handler.AppendFormatted(value.Substring(0, ")
                .Append(headTake.ToString(CultureInfo.InvariantCulture))
                .Append("));")
                .NewLine();
        }
        BuildAppendLiteral(builder, maskText);
        if (tailTake > 0)
        {
            builder
                .Indent()
                .Append("handler.AppendFormatted(value.Substring(value.Length - ")
                .Append(tail.ToString(CultureInfo.InvariantCulture));
            if (tailTake != tail)
            {
                builder.Append(", ").Append(tailTake.ToString(CultureInfo.InvariantCulture));
            }
            builder
                .Append("));")
                .NewLine();
        }
        builder.EndScope();
        builder.Indent().Append("else").NewLine();
        builder.BeginScope();
        BuildAppendLiteral(builder, shortText);
        builder.EndScope();

        builder.EndScope();

        builder.EndScope();
    }

    private static (int Head, string Text, int Tail) ParseMask(string mask)
    {
        var head = 0;
        while ((head < mask.Length) && (mask[head] == MaskKeepChar))
        {
            head++;
        }

        var tail = 0;
        while ((tail < (mask.Length - head)) && (mask[mask.Length - tail - 1] == MaskKeepChar))
        {
            tail++;
        }

        return (head, mask.Substring(head, mask.Length - head - tail), tail);
    }

    private static void BuildAppendMaxLength(SourceBuilder builder, OptionModel options, MemberModel member)
    {
        builder.BeginScope();

        BuildValueLocal(builder, member);
        BuildNullBranch(builder, options);

        builder
            .Indent()
            .Append("if (value.Length > ")
            .Append(member.MaxLength.ToString(CultureInfo.InvariantCulture))
            .Append(")")
            .NewLine();
        builder.BeginScope();
        builder
            .Indent()
            .Append("handler.AppendFormatted(value.Substring(0, ")
            .Append(member.MaxLength.ToString(CultureInfo.InvariantCulture))
            .Append("));")
            .NewLine();
        builder.EndScope();
        builder.Indent().Append("else").NewLine();
        builder.BeginScope();
        builder.Indent().Append("handler.AppendFormatted(value);").NewLine();
        builder.EndScope();

        builder.EndScope();

        builder.EndScope();
    }

    private static void BuildValueLocal(SourceBuilder builder, MemberModel member)
    {
        builder.Indent().Append("var value = ");
        if (!String.IsNullOrEmpty(member.Format))
        {
            builder
                .Append("this.")
                .Append(member.Name)
                .Append(" is global::System.IFormattable formattable ? formattable.ToString(\"")
                .Append(EscapeString(member.Format!))
                .Append("\", null) : this.")
                .Append(member.Name)
                .Append(member.IsNullAssignable ? "?.ToString();" : ".ToString();")
                .NewLine();
        }
        else
        {
            builder
                .Append("this.")
                .Append(member.Name)
                .Append(member.IsNullAssignable ? "?.ToString();" : ".ToString();")
                .NewLine();
        }
    }

    private static void BuildNullBranch(SourceBuilder builder, OptionModel options)
    {
        builder.Indent().Append("if (value is null)").NewLine();
        builder.BeginScope();
        if (options.Null == NullOption.Literal)
        {
            BuildAppendLiteral(builder, options.NullLiteral);
        }
        builder.EndScope();
        builder.Indent().Append("else").NewLine();
        builder.BeginScope();
    }

    private static void FlushLiteral(SourceBuilder builder, StringBuilder pending)
    {
        if (pending.Length == 0)
        {
            return;
        }

        BuildAppendLiteral(builder, pending.ToString());
        pending.Clear();
    }

    private static void BuildAppendLiteral(SourceBuilder builder, string literal)
    {
        if (literal.Length == 0)
        {
            return;
        }

        builder
            .Indent()
            .Append("handler.AppendLiteral(\"")
            .Append(EscapeString(literal))
            .Append("\");")
            .NewLine();
    }

    // ------------------------------------------------------------
    // Helper
    // ------------------------------------------------------------

    private static string Truncate(string value, int length) =>
        value.Length > length ? value.Substring(0, length) : value;

    private static string EscapeString(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string EscapeChar(char value) =>
        value switch
        {
            '\\' => "\\\\",
            '\'' => "\\'",
            _ => value.ToString()
        };

    private static string MakeFullName(INamedTypeSymbol symbol, string ns)
    {
        var buffer = new StringBuilder();

        if (!String.IsNullOrEmpty(ns))
        {
            buffer.Append(ns);
            buffer.Append('.');
        }

        var names = default(List<string>?);
        var containingSymbol = symbol.ContainingType;
        while (containingSymbol is not null)
        {
            names ??= [];
            names.Add(containingSymbol.Name);
            containingSymbol = containingSymbol.ContainingType;
        }

        if (names is not null)
        {
            names.Reverse();
            foreach (var name in names)
            {
                buffer.Append(name);
                buffer.Append('.');
            }
        }

        buffer.Append(symbol.Name);

        return buffer.ToString();
    }

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
    // Model
    // ------------------------------------------------------------

    // ReSharper disable UnusedMember.Local

    // Type name to write
    private enum TypeNameOption
    {
        None,
        Simple,
        Full
    }

    // Type arguments to include
    private enum TypeArgumentOption
    {
        None,
        Include
    }

    // Null value representation
    private enum NullOption
    {
        Empty,
        Literal
    }

    // Expand collection elements
    private enum CollectionOption
    {
        Raw,
        Expand
    }

    // Members to include
    private enum MemberKindOption
    {
        Property,
        PropertyAndField
    }

    // Bracket enclosing the body or the expanded elements
    private enum BracketOption
    {
        None,
        Brace,
        Parenthesis,
        Square,
        Angle
    }

    // Presence of a single space at that position
    private enum SpaceOption
    {
        None,
        Space
    }

    // ReSharper restore UnusedMember.Local

    private sealed record OptionModel(
        TypeNameOption TypeName,
        TypeArgumentOption TypeArgument,
        NullOption Null,
        string NullLiteral,
        CollectionOption Collection,
        int CollectionLimit,
        MemberKindOption Members,
        string OpenBracket,
        string CloseBracket,
        string InnerSpace,
        string TypeNameSpace,
        string Separator,
        string Assign,
        string CollectionOpenBracket,
        string CollectionCloseBracket,
        string CollectionInnerSpace,
        string CollectionSeparator);

    private sealed record ContainingTypeModel(
        string ClassName,
        bool IsValueType);

    private sealed record MemberModel(
        string Name,
        bool IsField,
        bool HasElements,
        bool IsNullAssignable,
        bool IsElementNullAssignable,
        string? Format,
        int MaxLength,
        char MaskChar,
        string? MaskPattern);

    private sealed record TypeModel(
        string Namespace,
        EquatableArray<ContainingTypeModel> ContainingTypes,
        string ClassName,
        bool IsValueType,
        string SimpleName,
        string FullName,
        EquatableArray<string> TypeParameters,
        EquatableArray<MemberModel> Members,
        EquatableArray<DiagnosticInfo> Diagnostics);
}
