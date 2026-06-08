namespace BunnyTail.CommonCode.Generator;

using System;
using System.Collections.Immutable;
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
            .SelectMany(static (x, _) => x is not null ? ImmutableArray.Create(x) : [])
            .Collect();

        context.RegisterImplementationSourceOutput(
            targetProvider,
            static (spc, types) => Execute(spc, types));
    }

    private static Result<EqualityTypeModel> GetTypeModel(GeneratorAttributeSyntaxContext context)
    {
        var syntax = (TypeDeclarationSyntax)context.TargetNode;
        var symbol = (INamedTypeSymbol)context.TargetSymbol;

        if (!syntax.Modifiers.Any(static x => x.IsKind(SyntaxKind.PartialKeyword)))
        {
            return Results.Error<EqualityTypeModel>(new DiagnosticInfo(Diagnostics.EqualityInvalidTypeDefinition, syntax.GetLocation(), symbol.Name));
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

        var attr = symbol.GetAttributes()
            .First(a => a.AttributeClass?.ToDisplayString() == GenerateAttributeName);

        var generateOperators = GetBoolArg(attr, "GenerateOperators") ?? true;
        var deepCollectionEquality = GetBoolArg(attr, "DeepCollectionEquality") ?? false;

        // 等価判定/ハッシュは、到達できる public プロパティを基底型まで辿って収集する (フラット仕様)
        // For equality / hash, collect reachable public properties walking up to base types (flat spec)
        var properties = new List<EqualityPropertyModel>();
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
                // 可視性 / IgnoreEquality 判定より前で登録するのは意図的: 派生の private / ignore な new 隠蔽でも、
                // this.<Name> から到達できない基底 public を誤って拾わず、コンパイルエラーや誤比較を防ぐ。
                // Since this.<Name> binds to the most-derived declaration, a hidden base property of the same name is not collected.
                // Registering before the visibility / IgnoreEquality check is intentional: even for a derived private / ignored new-hiding member,
                // this avoids wrongly picking up a base public unreachable from this.<Name>, preventing compile errors or incorrect comparisons.
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

                if (member.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == IgnoreAttributeName))
                {
                    continue;
                }

                var isCollection = IsCollectionType(member.Type);
                properties.Add(new EqualityPropertyModel(
                    member.Name,
                    isCollection,
                    member.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
            }
            currentSymbol = currentSymbol.BaseType;
        }

        if (properties.Count == 0)
        {
            return Results.Error<EqualityTypeModel>(new DiagnosticInfo(Diagnostics.EqualityNoProperties, syntax.GetLocation(), symbol.Name));
        }

        return Results.Success(new EqualityTypeModel(
            ns,
            new EquatableArray<ContainingTypeModel>(containingTypes?.ToArray() ?? []),
            symbol.GetClassName(),
            symbol.IsValueType,
            symbol.IsSealed,
            generateOperators,
            deepCollectionEquality,
            new EquatableArray<EqualityPropertyModel>(properties.ToArray())));
    }

    private static bool? GetBoolArg(AttributeData attr, string name)
    {
        var arg = attr.NamedArguments.FirstOrDefault(na => na.Key == name);
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

    private static bool IsCollectionType(ITypeSymbol typeSymbol)
    {
        if (typeSymbol.SpecialType == SpecialType.System_String)
        {
            return false;
        }

        if (typeSymbol is IArrayTypeSymbol)
        {
            return true;
        }

        foreach (var iface in typeSymbol.AllInterfaces)
        {
            if (iface.IsGenericType && iface.ConstructedFrom.ToDisplayString() == "System.Collections.Generic.IEnumerable<T>")
            {
                return true;
            }
        }

        return false;
    }

    // ------------------------------------------------------------
    // Execute
    // ------------------------------------------------------------

    private static void Execute(SourceProductionContext context, ImmutableArray<Result<EqualityTypeModel>> types)
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
            BuildSource(builder, type);

            var filename = MakeFilename(type.Namespace, type.ContainingTypes, type.ClassName, "Equality");
            context.AddSource(filename, SourceText.From(builder.ToString(), Encoding.UTF8));
        }
    }

    private static void BuildSource(SourceBuilder builder, EqualityTypeModel type)
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

        // Equals(T?)
        builder.Indent()
            .Append("public bool Equals(")
            .Append(type.ClassName)
            .Append("? other)")
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
                builder.NewLine();
                builder.Indent().Append("    && ");
            }

            if (prop.IsCollection && type.DeepCollectionEquality)
            {
                builder
                    .Append("SequenceEqualOrBothNull(this.")
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
            if (prop.IsCollection && type.DeepCollectionEquality)
            {
                builder.Indent().Append("if (this.").Append(prop.Name).Append(" is not null)").NewLine();
                builder.BeginScope();
                builder.Indent().Append("foreach (var item in this.").Append(prop.Name).Append(")").NewLine();
                builder.BeginScope();
                builder.Indent().Append("hash.Add(item);").NewLine();
                builder.EndScope();
                builder.EndScope();
            }
            else
            {
                builder.Indent().Append("hash.Add(this.").Append(prop.Name).Append(");").NewLine();
            }
        }
        builder.Indent().Append("return hash.ToHashCode();").NewLine();
        builder.EndScope();

        // operators
        if (type.GenerateOperators && !type.IsValueType)
        {
            builder.NewLine();
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

        // SequenceEqualOrBothNull helper
        if (type.DeepCollectionEquality && properties.Any(p => p.IsCollection))
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

        builder.EndScope();

        for (var i = 0; i < containingTypes.Count; i++)
        {
            builder.EndScope();
        }
    }

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

    // TODO

    private sealed record ContainingTypeModel(
        string ClassName,
        bool IsValueType);

    private sealed record EqualityTypeModel(
        string Namespace,
        EquatableArray<ContainingTypeModel> ContainingTypes,
        string ClassName,
        bool IsValueType,
        bool IsSealed,
        bool GenerateOperators,
        bool DeepCollectionEquality,
        EquatableArray<EqualityPropertyModel> Properties);

    private sealed record EqualityPropertyModel(
        string Name,
        bool IsCollection,
        string TypeName);
}
