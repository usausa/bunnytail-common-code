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

    // C# のキーワードを持つ型は typeof(int) のように別名で出力する
    // Types that have a C# keyword are written using the keyword, such as int instead of Int32
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
            .Select(static (provider, _) => ResolveSettings(GetOptions(provider)));

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

    private static bool IsTypeSyntax(SyntaxNode node) =>
        node is ClassDeclarationSyntax or StructDeclarationSyntax;

    // ------------------------------------------------------------
    // Option
    // ------------------------------------------------------------

    private static OptionModel GetOptions(AnalyzerConfigOptionsProvider provider)
    {
        var options = provider.GlobalOptions;
        return new OptionModel(
            GetEnumOption<StyleOption>(options, "Style"),
            GetEnumOption<TypeNameOption>(options, "TypeName"),
            GetEnumOption<TypeArgumentOption>(options, "TypeArgument"),
            GetEnumOption<NullOption>(options, "Null"),
            GetStringOption(options, "NullLiteral"),
            GetEnumOption<CollectionOption>(options, "Collection"),
            GetIntOption(options, "CollectionLimit"),
            GetEnumOption<MemberKindOption>(options, "Members"),
            GetEnumOption<BracketOption>(options, "Bracket"),
            GetStringOption(options, "OpenBracket"),
            GetStringOption(options, "CloseBracket"),
            GetEnumOption<SpaceOption>(options, "InnerSpace"),
            GetEnumOption<SpaceOption>(options, "TypeNameSpace"),
            GetStringOption(options, "Separator"),
            GetStringOption(options, "Assign"),
            GetEnumOption<BracketOption>(options, "CollectionBracket"),
            GetStringOption(options, "CollectionOpenBracket"),
            GetStringOption(options, "CollectionCloseBracket"),
            GetEnumOption<SpaceOption>(options, "CollectionInnerSpace"),
            GetStringOption(options, "CollectionSeparator"));
    }

    private static string? GetStringOption(AnalyzerConfigOptions options, string name) =>
        Unquote(options.GetValue<string?>(OptionPrefix + name));

    // MSBuild はプロパティ値の前後の空白を除去するため、引用符で囲むことで空白を含む値を指定できるようにする。
    // MSBuild trims the surrounding whitespace of a property value, so quoting allows a value that contains it.
    private static string? Unquote(string? value) =>
        (value is not null) && (value.Length >= 2) && (value[0] == '"') && (value[value.Length - 1] == '"')
            ? value.Substring(1, value.Length - 2)
            : value;

    private static T GetEnumOption<T>(AnalyzerConfigOptions options, string name)
        where T : struct, Enum
    {
        var value = options.GetValue<string?>(OptionPrefix + name);
        return !String.IsNullOrEmpty(value) && Enum.TryParse<T>(value, true, out var result) ? result : default;
    }

    private static int GetIntOption(AnalyzerConfigOptions options, string name)
    {
        var value = options.GetValue<string?>(OptionPrefix + name);
        return !String.IsNullOrEmpty(value) && Int32.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : 0;
    }

    // ------------------------------------------------------------
    // Settings
    // ------------------------------------------------------------

    private static SettingsModel ResolveSettings(OptionModel options)
    {
        // 未指定の項目はプリセットの値を使用する。Record スタイルとの差があるのは以下の 4 項目のみ。
        // An unspecified option falls back to the preset value. Only the following four differ from the record style.
        var record = options.Style == StyleOption.Record;

        var typeArgument = options.TypeArgument != TypeArgumentOption.Inherit
            ? options.TypeArgument
            : record ? TypeArgumentOption.None : TypeArgumentOption.Include;
        var nullMode = options.Null != NullOption.Inherit
            ? options.Null
            : record ? NullOption.Empty : NullOption.Literal;
        var collection = options.Collection != CollectionOption.Inherit
            ? options.Collection
            : record ? CollectionOption.Raw : CollectionOption.Expand;
        var members = options.Members != MemberKindOption.Inherit
            ? options.Members
            : record ? MemberKindOption.PropertyAndField : MemberKindOption.Property;

        var typeName = options.TypeName != TypeNameOption.Inherit ? options.TypeName : TypeNameOption.Simple;
        var nullLiteral = !String.IsNullOrEmpty(options.NullLiteral) ? options.NullLiteral! : "null";
        var collectionLimit = options.CollectionLimit != 0 ? options.CollectionLimit : -1;
        var innerSpace = options.InnerSpace != SpaceOption.Inherit ? options.InnerSpace : SpaceOption.Space;
        var typeNameSpace = options.TypeNameSpace != SpaceOption.Inherit ? options.TypeNameSpace : SpaceOption.Space;
        var separator = !String.IsNullOrEmpty(options.Separator) ? options.Separator! : ", ";
        var assign = !String.IsNullOrEmpty(options.Assign) ? options.Assign! : " = ";
        var collectionInnerSpace = options.CollectionInnerSpace != SpaceOption.Inherit ? options.CollectionInnerSpace : SpaceOption.None;
        var collectionSeparator = !String.IsNullOrEmpty(options.CollectionSeparator) ? options.CollectionSeparator! : ", ";

        var bracket = options.Bracket != BracketOption.Inherit ? options.Bracket : BracketOption.Brace;
        var openBracket = ResolveBracket(bracket, options.OpenBracket, true);
        var closeBracket = ResolveBracket(bracket, options.CloseBracket, false);
        var hasBracket = (openBracket.Length > 0) || (closeBracket.Length > 0);

        var collectionBracket = options.CollectionBracket != BracketOption.Inherit ? options.CollectionBracket : BracketOption.Square;
        var collectionOpenBracket = ResolveBracket(collectionBracket, options.CollectionOpenBracket, true);
        var collectionCloseBracket = ResolveBracket(collectionBracket, options.CollectionCloseBracket, false);
        var hasCollectionBracket = (collectionOpenBracket.Length > 0) || (collectionCloseBracket.Length > 0);

        return new SettingsModel(
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

    private static string ResolveBracket(BracketOption bracket, string? value, bool open)
    {
        if (!String.IsNullOrEmpty(value))
        {
            return value!;
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

        return Results.Success(new TypeModel(
            ns,
            new EquatableArray<ContainingTypeModel>(containingTypes?.ToArray() ?? []),
            symbol.GetClassName(),
            symbol.Name,
            MakeFullName(symbol, ns),
            new EquatableArray<string>(symbol.TypeParameters.Select(static x => x.Name).ToArray()),
            symbol.IsValueType,
            new EquatableArray<MemberModel>(CollectMembers(symbol).ToArray())));
    }

    private static List<MemberModel> CollectMembers(INamedTypeSymbol symbol)
    {
        var levels = new List<List<MemberModel>>();
        var seenNames = new HashSet<string>(StringComparer.Ordinal);
        var currentSymbol = symbol;
        while (currentSymbol is not null)
        {
            var members = new List<MemberModel>();
            foreach (var member in currentSymbol.GetMembers())
            {
                // static メンバは this.<Name> でアクセスできないため対象外
                // Static members are excluded because they cannot be accessed via this.<Name>
                if (member.IsStatic)
                {
                    continue;
                }

                if (member is IPropertySymbol property)
                {
                    // インデクサは this.<Name> でアクセスできないため対象外
                    // Indexers are excluded because they cannot be accessed via this.<Name>
                    if (property.IsIndexer)
                    {
                        continue;
                    }

                    // this.<Name> は最派生の宣言に束縛されるため、隠蔽された基底側の同名メンバは収集しない。
                    // 可視性 / IgnoreToString 判定より前で登録するのは意図的: 派生の private / ignore な new 隠蔽でも、
                    // this.<Name> から到達できない基底 public を誤って拾わず、隠蔽 / ignore したメンバの値を出力しない。
                    // Since this.<Name> binds to the most-derived declaration, a hidden base member of the same name is not collected.
                    // Registering before the visibility / IgnoreToString check is intentional: even for a derived private / ignored new-hiding member,
                    // this avoids wrongly picking up a base public unreachable from this.<Name>, and avoids outputting the value of a hidden / ignored member.
                    if (!seenNames.Add(property.Name))
                    {
                        continue;
                    }

                    if ((property.DeclaredAccessibility != Accessibility.Public) ||
                        (property.GetMethod is null) ||
                        property.IsWriteOnly ||
                        HasIgnore(property))
                    {
                        continue;
                    }

                    members.Add(GetMemberModel(property, property.Type, false));
                }
                else if (member is IFieldSymbol field)
                {
                    // 自動プロパティのバッキングフィールド等、コンパイラが生成したフィールドは対象外
                    // Compiler generated fields such as auto property backing fields are excluded
                    if (field.IsImplicitlyDeclared || (field.AssociatedSymbol is not null))
                    {
                        continue;
                    }

                    if (!seenNames.Add(field.Name))
                    {
                        continue;
                    }

                    if ((field.DeclaredAccessibility != Accessibility.Public) || HasIgnore(field))
                    {
                        continue;
                    }

                    members.Add(GetMemberModel(field, field.Type, true));
                }
            }

            levels.Add(members);
            currentSymbol = currentSymbol.BaseType;
        }

        // record と同じく基底型のメンバを先に出力する
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

    private static MemberModel GetMemberModel(ISymbol symbol, ITypeSymbol type, bool isField)
    {
        var (hasElements, isNullAssignable, isElementNullAssignable) = GetMemberType(type);

        var format = default(string?);
        var maskPattern = default(string?);
        var maskChar = '\0';
        var maxLength = 0;

        var attr = symbol.GetAttributes()
            .FirstOrDefault(static x => x.AttributeClass?.ToDisplayString() == FormatAttributeName);
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

            foreach (var @interface in typeSymbol.AllInterfaces)
            {
                if (@interface.IsGenericType && (@interface.ConstructedFrom.ToDisplayString() == GenericEnumerableName))
                {
                    var elementType = @interface.TypeArguments[0];
                    return (true, isNullAssignable, elementType.IsReferenceType || elementType.IsGenericType());
                }
            }
        }

        return (false, isNullAssignable, false);
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

    private static void Execute(SourceProductionContext context, SettingsModel settings, TypeModel type)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        var builder = new SourceBuilder();
        BuildSource(builder, settings, type);

        var filename = MakeFilename(type.Namespace, type.ContainingTypes, type.ClassName);
        var source = builder.ToString();
        context.AddSource(filename, SourceText.From(source, Encoding.UTF8));
    }

    private static void BuildSource(SourceBuilder builder, SettingsModel settings, TypeModel type)
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
        BuildMember(builder, settings, type);

        builder.EndScope();

        // end containing types
        for (var i = 0; i < containingTypes.Count; i++)
        {
            builder.EndScope();
        }
    }

    private static void BuildMember(SourceBuilder builder, SettingsModel settings, TypeModel type)
    {
        var members = new List<MemberModel>();
        // ReSharper disable once LoopCanBeConvertedToQuery
        foreach (var member in type.Members)
        {
            if (member.IsField && (settings.Members != MemberKindOption.PropertyAndField))
            {
                continue;
            }

            members.Add(member);
        }

        var typeName = settings.TypeName switch
        {
            TypeNameOption.Simple => type.SimpleName,
            TypeNameOption.Full => type.FullName,
            _ => string.Empty
        };

        // 型引数はコンパイル時に確定しないため、typeof(T) から実行時に組み立てて static readonly に保持する。
        // Since type arguments are not known at compile time, the name is built from typeof(T) at runtime and held in a static readonly field.
        var useTypeArgument = (settings.TypeArgument == TypeArgumentOption.Include) &&
                              (settings.TypeName != TypeNameOption.None) &&
                              (type.TypeParameters.Count > 0);

        // 先頭リテラルは型名に続く部分。型引数を使う場合はキャッシュへ畳み込む。
        // The head literal follows the type name. It is folded into the cache when type arguments are used.
        var head = new StringBuilder();
        head.Append(settings.TypeNameSpace).Append(settings.OpenBracket).Append(settings.InnerSpace);
        if (members.Count == 0)
        {
            head.Append(settings.CloseBracket);
        }
        else
        {
            head.Append(members[0].Name).Append(settings.Assign);
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

        // 出力対象が無い場合は組み立て済みの文字列を返す。開始括弧と終了括弧の内側スペースは 1 つに畳まれる。
        // When there is no member to output, a prepared string is returned. The inner space is collapsed into one.
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

        BuildMemberValue(builder, settings, members[0]);

        var pending = new StringBuilder();
        for (var i = 1; i < members.Count; i++)
        {
            var member = members[i];
            pending.Append(settings.Separator).Append(member.Name).Append(settings.Assign);

            FlushLiteral(builder, pending);

            BuildMemberValue(builder, settings, member);
        }

        pending.Append(settings.InnerSpace).Append(settings.CloseBracket);
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

    private static void BuildMemberValue(SourceBuilder builder, SettingsModel settings, MemberModel member)
    {
        // マスク指定はコレクション展開より優先する。展開すると要素の値がそのまま出力されてしまうため。
        // Mask takes precedence over collection expansion, otherwise the element values would be written as is.
        if (!String.IsNullOrEmpty(member.MaskPattern))
        {
            BuildAppendMaskPattern(builder, settings, member);
        }
        else if (member.MaskChar != '\0')
        {
            BuildAppendMaskChar(builder, settings, member);
        }
        else if (member.HasElements && (settings.Collection == CollectionOption.Expand))
        {
            BuildAppendCollection(builder, settings, member);
        }
        else if (member.MaxLength > 0)
        {
            BuildAppendMaxLength(builder, settings, member);
        }
        else if (member.IsNullAssignable && (settings.Null == NullOption.Literal))
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

            BuildAppendLiteral(builder, settings.NullLiteral);

            builder.EndScope();
        }
        else
        {
            // null の場合 AppendFormatted は何も追加しないため、record と同じく空文字になる
            // AppendFormatted appends nothing for null, which results in an empty string same as record
            BuildAppendFormatted(builder, member);
        }
    }

    private static void BuildAppendCollection(SourceBuilder builder, SettingsModel settings, MemberModel member)
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

            BuildCollectionBody(builder, settings, member);

            builder.EndScope();

            if (settings.Null == NullOption.Literal)
            {
                builder
                    .Indent()
                    .Append("else")
                    .NewLine();
                builder.BeginScope();

                BuildAppendLiteral(builder, settings.NullLiteral);

                builder.EndScope();
            }
        }
        else
        {
            // 要素の列挙に使う変数名が同一メソッド内で衝突しないようにスコープを作る
            // A scope is created so that the variable used for enumeration does not conflict within the same method
            builder.BeginScope();

            BuildCollectionBody(builder, settings, member);

            builder.EndScope();
        }
    }

    private static void BuildCollectionBody(SourceBuilder builder, SettingsModel settings, MemberModel member)
    {
        var limited = settings.CollectionLimit > 0;
        var hasInnerSpace = settings.CollectionInnerSpace.Length > 0;

        BuildAppendLiteral(builder, settings.CollectionOpenBracket);

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
            builder.Indent().Append("if (itemIndex > 0) { handler.AppendLiteral(\"").Append(EscapeString(settings.CollectionSeparator)).Append("\"); }");
            if (hasInnerSpace)
            {
                builder.Append(" else { handler.AppendLiteral(\"").Append(EscapeString(settings.CollectionInnerSpace)).Append("\"); }");
            }
            builder.NewLine();

            if (limited)
            {
                builder
                    .Indent()
                    .Append("if (itemIndex == ")
                    .Append(settings.CollectionLimit.ToString(CultureInfo.InvariantCulture))
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
                .Append(EscapeString(settings.CollectionSeparator))
                .Append("\"); }")
                .NewLine();
        }

        if (member.IsElementNullAssignable && (settings.Null == NullOption.Literal))
        {
            builder
                .Indent()
                .Append("if (item is not null) { handler.AppendFormatted(item); } else { handler.AppendLiteral(\"")
                .Append(EscapeString(settings.NullLiteral))
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
                .Append(EscapeString(settings.CollectionInnerSpace))
                .Append("\"); }")
                .NewLine();
        }

        BuildAppendLiteral(builder, settings.CollectionCloseBracket);
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

    // Mask の先頭 / 末尾に並ぶ '#' は元の値を残す文字数を表し、その間の文字列がマスク文字列になる。
    // A leading / trailing run of '#' in Mask is the number of original characters kept, and the part between them is the mask text.
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

    // 値全体を 1 文字で埋めるため、元の文字数だけが必要になる。MaxLength は埋める文字数の上限になる。
    // The whole value is filled with a single character, so only the original length is needed. MaxLength caps the filled length.
    private static void BuildAppendMaskChar(SourceBuilder builder, SettingsModel settings, MemberModel member)
    {
        builder.BeginScope();

        BuildValueLocal(builder, member);
        BuildNullBranch(builder, settings);

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

    private static void BuildAppendMaskPattern(SourceBuilder builder, SettingsModel settings, MemberModel member)
    {
        var (head, text, tail) = ParseMask(member.MaskPattern!);
        var keep = head + tail;

        // マスク適用後に MaxLength を適用する。マスク後の出力長はコンパイル時に確定するため、生成時に切り詰める。
        // MaxLength is applied after masking. The masked length is known at compile time, so it is truncated during generation.
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

        // 元の文字を残さない場合は値を文字列化する必要が無く、null 判定だけで済む
        // When no original character is kept, the value does not need to be stringified and only the null check is required
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

            if (settings.Null == NullOption.Literal)
            {
                builder.Indent().Append("else").NewLine();
                builder.BeginScope();
                BuildAppendLiteral(builder, settings.NullLiteral);
                builder.EndScope();
            }

            return;
        }

        builder.BeginScope();

        BuildValueLocal(builder, member);
        BuildNullBranch(builder, settings);

        // 残す文字数に満たない値は元の文字が漏れないようマスク文字列だけを出力する
        // A value not longer than the kept length is written as the mask text only, so that no original character leaks
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

    private static string Truncate(string value, int length) =>
        value.Length > length ? value.Substring(0, length) : value;

    private static void BuildAppendMaxLength(SourceBuilder builder, SettingsModel settings, MemberModel member)
    {
        builder.BeginScope();

        BuildValueLocal(builder, member);
        BuildNullBranch(builder, settings);

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

    private static void BuildNullBranch(SourceBuilder builder, SettingsModel settings)
    {
        builder.Indent().Append("if (value is null)").NewLine();
        builder.BeginScope();
        if (settings.Null == NullOption.Literal)
        {
            BuildAppendLiteral(builder, settings.NullLiteral);
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
    // Models
    // ------------------------------------------------------------

    // ReSharper disable once UnusedMember.Local
    private enum StyleOption
    {
        Default = 0,
        Record
    }

    private enum TypeNameOption
    {
        Inherit = 0,
        None,
        Simple,
        Full
    }

    private enum TypeArgumentOption
    {
        Inherit = 0,
        None,
        Include
    }

    private enum NullOption
    {
        Inherit = 0,
        Empty,
        Literal
    }

    private enum CollectionOption
    {
        Inherit = 0,
        Raw,
        Expand
    }

    private enum MemberKindOption
    {
        Inherit = 0,
        Property,
        PropertyAndField
    }

    // ReSharper disable once UnusedMember.Local
    private enum BracketOption
    {
        Inherit = 0,
        None,
        Brace,
        Parenthesis,
        Square,
        Angle
    }

    private enum SpaceOption
    {
        Inherit = 0,
        None,
        Space
    }

    private sealed record OptionModel(
        StyleOption Style,
        TypeNameOption TypeName,
        TypeArgumentOption TypeArgument,
        NullOption Null,
        string? NullLiteral,
        CollectionOption Collection,
        int CollectionLimit,
        MemberKindOption Members,
        BracketOption Bracket,
        string? OpenBracket,
        string? CloseBracket,
        SpaceOption InnerSpace,
        SpaceOption TypeNameSpace,
        string? Separator,
        string? Assign,
        BracketOption CollectionBracket,
        string? CollectionOpenBracket,
        string? CollectionCloseBracket,
        SpaceOption CollectionInnerSpace,
        string? CollectionSeparator);

    private sealed record SettingsModel(
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

    private sealed record TypeModel(
        string Namespace,
        EquatableArray<ContainingTypeModel> ContainingTypes,
        string ClassName,
        string SimpleName,
        string FullName,
        EquatableArray<string> TypeParameters,
        bool IsValueType,
        EquatableArray<MemberModel> Members);

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
}
