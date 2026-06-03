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
                static (node, _) => node is ClassDeclarationSyntax or StructDeclarationSyntax,
                static (ctx, _) => GetTypeModel(ctx))
            .SelectMany(static (x, _) => x is not null ? ImmutableArray.Create(x) : [])
            .Collect();

        context.RegisterImplementationSourceOutput(
            targetProvider,
            static (spc, types) => Execute(spc, types));
    }

    private static Result<CompareToTypeModel> GetTypeModel(GeneratorAttributeSyntaxContext context)
    {
        var syntax = (TypeDeclarationSyntax)context.TargetNode;
        var symbol = (INamedTypeSymbol)context.TargetSymbol;

        if (!syntax.Modifiers.Any(static x => x.IsKind(SyntaxKind.PartialKeyword)))
        {
            return Results.Error<CompareToTypeModel>(new DiagnosticInfo(Diagnostics.CompareToInvalidTypeDefinition, syntax.GetLocation(), symbol.Name));
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

        var attr = symbol.GetAttributes()
            .First(a => a.AttributeClass?.ToDisplayString() == GenerateAttributeName);

        var generateOperators = GetBoolArg(attr, "GenerateOperators") ?? true;

        var keys = new List<(int Order, string Name, string TypeName)>();
        foreach (var member in symbol.GetMembers().OfType<IPropertySymbol>())
        {
            // インデクサは this.<Name> でアクセスできないため対象外
            if (member.IsIndexer)
            {
                continue;
            }

            if (member.DeclaredAccessibility != Accessibility.Public)
            {
                continue;
            }

            var keyAttr = member.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == CompareKeyAttributeName);
            if (keyAttr == null)
            {
                continue;
            }
            var order = GetIntArg(keyAttr, "Order") ?? 0;
            keys.Add((order, member.Name, member.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
        }

        if (keys.Count == 0)
        {
            return Results.Error<CompareToTypeModel>(new DiagnosticInfo(Diagnostics.CompareToNoKeys, syntax.GetLocation(), symbol.Name));
        }

        keys.Sort(static (a, b) => a.Order.CompareTo(b.Order));

        return Results.Success(new CompareToTypeModel(
            ns,
            new EquatableArray<ContainingTypeModel>(containingTypes?.ToArray() ?? []),
            symbol.GetClassName(),
            symbol.IsValueType,
            generateOperators,
            new EquatableArray<CompareKeyModel>(keys.Select(static k => new CompareKeyModel(k.Name, k.TypeName)).ToArray())));
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

    private static int? GetIntArg(AttributeData attr, string name)
    {
        var arg = attr.NamedArguments.FirstOrDefault(na => na.Key == name);
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
    // Execute
    // ------------------------------------------------------------

    private static void Execute(SourceProductionContext context, ImmutableArray<Result<CompareToTypeModel>> types)
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

            var filename = MakeFilename(type.Namespace, type.ContainingTypes, type.ClassName, "CompareTo");
            context.AddSource(filename, SourceText.From(builder.ToString(), Encoding.UTF8));
        }
    }

    private static void BuildSource(SourceBuilder builder, CompareToTypeModel type)
    {
        var containingTypes = type.ContainingTypes;
        var keys = type.Keys;

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
            .Append(" : global::System.IComparable<")
            .Append(type.ClassName)
            .Append(">")
            .NewLine();
        builder.BeginScope();

        // CompareTo(T?)
        builder.Indent()
            .Append("public int CompareTo(")
            .Append(type.ClassName)
            .Append("? other)")
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
            builder.Indent()
                .Append("result = global::System.Collections.Generic.Comparer<")
                .Append(key.TypeName)
                .Append(">.Default.Compare(this.")
                .Append(key.Name)
                .Append(", other.")
                .Append(key.Name)
                .Append(");")
                .NewLine();
            builder.Indent().Append("if (result != 0)").NewLine();
            builder.BeginScope();
            builder.Indent().Append("return result;").NewLine();
            builder.EndScope();
        }

        builder.Indent().Append("return 0;").NewLine();
        builder.EndScope();

        // operators
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

    private sealed record ContainingTypeModel(
        string ClassName,
        bool IsValueType);

    private sealed record CompareToTypeModel(
        string Namespace,
        EquatableArray<ContainingTypeModel> ContainingTypes,
        string ClassName,
        bool IsValueType,
        bool GenerateOperators,
        EquatableArray<CompareKeyModel> Keys);

    private sealed record CompareKeyModel(
        string Name,
        string TypeName);
}
