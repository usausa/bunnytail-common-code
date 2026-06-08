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
public sealed class DeepCloneGenerator : IIncrementalGenerator
{
    // ReSharper disable InconsistentNaming
    private const string GenerateAttributeName = "BunnyTail.CommonCode.GenerateDeepCloneAttribute";
    private const string ShallowCloneAttributeName = "BunnyTail.CommonCode.ShallowCloneAttribute";
    private const string CloneIgnoreAttributeName = "BunnyTail.CommonCode.IgnoreCloneAttribute";
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
            .SelectMany(static (x, _) => x is not null ? ImmutableArray.Create(x) : [])
            .Collect();

        context.RegisterImplementationSourceOutput(
            targetProvider,
            static (spc, types) => Execute(spc, types));
    }

    private static Result<DeepCloneTypeModel> GetTypeModel(GeneratorAttributeSyntaxContext context)
    {
        var syntax = (TypeDeclarationSyntax)context.TargetNode;
        var symbol = (INamedTypeSymbol)context.TargetSymbol;

        if (!syntax.Modifiers.Any(static x => x.IsKind(SyntaxKind.PartialKeyword)))
        {
            return Results.Error<DeepCloneTypeModel>(new DiagnosticInfo(Diagnostics.DeepCloneInvalidTypeDefinition, syntax.GetLocation(), symbol.Name));
        }

        // IDeepCloneable<T> を実装しているか確認
        // Check whether IDeepCloneable<T> is implemented
        var implementsDeepCloneable = symbol.AllInterfaces.Any(i =>
            i.IsGenericType && i.ConstructedFrom.ToDisplayString() == IDeepCloneableName);
        if (!implementsDeepCloneable)
        {
            return Results.Error<DeepCloneTypeModel>(new DiagnosticInfo(Diagnostics.DeepCloneNotImplementIDeepCloneable, syntax.GetLocation(), symbol.Name));
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

        var properties = new List<ClonePropertyModel>();
        var diagnostics = new List<DiagnosticInfo>();
        foreach (var member in symbol.GetMembers().OfType<IPropertySymbol>())
        {
            // インデクサは clone.<Name> で代入できないため対象外
            // Indexers are excluded because they cannot be assigned via clone.<Name>
            if (member.IsIndexer)
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

            // set / init のいずれも持たない (get-only) プロパティは代入できないため対象外
            // Properties with neither set nor init (get-only) are excluded because they cannot be assigned
            if (member.SetMethod is null)
            {
                continue;
            }

            if (member.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == CloneIgnoreAttributeName))
            {
                continue;
            }

            var shallow = member.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == ShallowCloneAttributeName);
            var cloneStrategy = shallow
                ? CloneStrategy.Shallow
                : GetCloneStrategy(member.Type);

            if (!shallow && cloneStrategy == CloneStrategy.Unknown)
            {
                // ディープクローン手段が不明な参照型はシャローコピーに落とすが、利用者へ診断で通知する
                // Reference types with no known deep-clone method fall back to a shallow copy, but notify the user via a diagnostic
                diagnostics.Add(new DiagnosticInfo(
                    Diagnostics.DeepClonePropertyMissingDeepClone,
                    member.Locations.FirstOrDefault() ?? syntax.GetLocation(),
                    member.Name,
                    member.Type.ToDisplayString()));
                cloneStrategy = CloneStrategy.Shallow;
            }

            properties.Add(new ClonePropertyModel(
                member.Name,
                member.Type.ToDisplayString(),
                cloneStrategy,
                member.Type.IsReferenceType,
                member.SetMethod.IsInitOnly));
        }

        var model = new DeepCloneTypeModel(
            ns,
            new EquatableArray<ContainingTypeModel>(containingTypes?.ToArray() ?? []),
            symbol.GetClassName(),
            symbol.IsValueType,
            new EquatableArray<ClonePropertyModel>(properties.ToArray()));

        return diagnostics.Count == 0
            ? Results.Success(model)
            : new Result<DeepCloneTypeModel>(model, new EquatableArray<DiagnosticInfo>(diagnostics.ToArray()));
    }

    private static CloneStrategy GetCloneStrategy(ITypeSymbol typeSymbol)
    {
        if (typeSymbol.IsValueType || typeSymbol.SpecialType == SpecialType.System_String)
        {
            return CloneStrategy.Direct;
        }

        if (typeSymbol.AllInterfaces.Any(i => i.IsGenericType && i.ConstructedFrom.ToDisplayString() == IDeepCloneableName))
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
    // Execute
    // ------------------------------------------------------------

    private static void Execute(SourceProductionContext context, ImmutableArray<Result<DeepCloneTypeModel>> types)
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

            var filename = MakeFilename(type.Namespace, type.ContainingTypes, type.ClassName, "DeepClone");
            context.AddSource(filename, SourceText.From(builder.ToString(), Encoding.UTF8));
        }
    }

    private static void BuildSource(SourceBuilder builder, DeepCloneTypeModel type)
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

        // init 専用プロパティはコンストラクション後に代入できないため、オブジェクト初期化子で設定する
        // init-only properties cannot be assigned after construction, so set them via the object initializer
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

        // set 可能なプロパティは代入で設定する
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

    private static void BuildCloneExpression(SourceBuilder builder, ClonePropertyModel prop)
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
                        .Append(prop.TypeDisplayName)
                        .Append(")((global::System.Array)this.")
                        .Append(prop.Name)
                        .Append(").Clone()");
                }
                else
                {
                    builder
                        .Append("(")
                        .Append(prop.TypeDisplayName)
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
                        .Append(prop.TypeDisplayName)
                        .Append("(this.").Append(prop.Name).Append(")");
                }
                else
                {
                    builder
                        .Append("new ").Append(prop.TypeDisplayName)
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

    private sealed record DeepCloneTypeModel(
        string Namespace,
        EquatableArray<ContainingTypeModel> ContainingTypes,
        string ClassName,
        bool IsValueType,
        EquatableArray<ClonePropertyModel> Properties);

    private sealed record ClonePropertyModel(
        string Name,
        string TypeDisplayName,
        CloneStrategy Strategy,
        bool IsReferenceType,
        bool RequiresInit);
}
