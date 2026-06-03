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
public sealed class DelegateToGenerator : IIncrementalGenerator
{
    private const string GenerateAttributeName = "BunnyTail.CommonCode.GenerateDelegateToAttribute";
    private const string DelegateToAttributeName = "BunnyTail.CommonCode.DelegateToAttribute";

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

    private static Result<DelegateToTypeModel> GetTypeModel(GeneratorAttributeSyntaxContext context)
    {
        var syntax = (TypeDeclarationSyntax)context.TargetNode;
        var symbol = (INamedTypeSymbol)context.TargetSymbol;

        if (!syntax.Modifiers.Any(static x => x.IsKind(SyntaxKind.PartialKeyword)))
        {
            return Results.Error<DelegateToTypeModel>(new DiagnosticInfo(Diagnostics.DelegateToInvalidTypeDefinition, syntax.GetLocation(), symbol.Name));
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

        var delegateGroups = new List<DelegateGroupModel>();

        // 既にクラスが実装しているメンバ名を収集 (手書きで実装しているものはスキップ)
        var existingMemberNames = new HashSet<string>(
            symbol.GetMembers()
                .Where(m => (m is IMethodSymbol ms && ms.MethodKind == MethodKind.Ordinary && !m.IsAbstract) ||
                            (m is IPropertySymbol ps && !ps.IsAbstract))
                .Select(m => m.Name));

        foreach (var member in symbol.GetMembers())
        {
            ITypeSymbol? memberType = null;
            string? memberName = null;
            ImmutableArray<AttributeData> memberAttrs = default;

            if (member is IFieldSymbol field)
            {
                if (!field.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == DelegateToAttributeName))
                {
                    continue;
                }

                memberType = field.Type;
                memberName = field.Name;
                memberAttrs = field.GetAttributes();
            }
            else if (member is IPropertySymbol prop)
            {
                if (!prop.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == DelegateToAttributeName))
                {
                    continue;
                }

                memberType = prop.Type;
                memberName = prop.Name;
                memberAttrs = prop.GetAttributes();
            }

            if (memberType == null || memberName == null)
            {
                continue;
            }

            // InterfaceType 引数は型引数として取れないため、メンバ型を使用
            var interfaceType = memberType;

            // インターフェース型に限定 (具象クラスの場合、実装インターフェースを使う)
            IEnumerable<ITypeSymbol> interfacesToDelegate;
            if (interfaceType.TypeKind == TypeKind.Interface)
            {
                interfacesToDelegate = [interfaceType];
            }
            else if (interfaceType is INamedTypeSymbol)
            {
                // 具象型の場合、クラス自身が宣言しているインターフェースを使う
                interfacesToDelegate = symbol.Interfaces;
            }
            else
            {
                continue;
            }

            var methods = new List<DelegateMethodModel>();

            foreach (var iface in interfacesToDelegate)
            {
                IEnumerable<ISymbol> allMembers = iface.GetMembers();
                if (iface is INamedTypeSymbol namedIface)
                {
                    allMembers = namedIface.AllInterfaces
                        .SelectMany(i => i.GetMembers())
                        .Concat(allMembers);
                }

                foreach (var ifaceMember in allMembers)
                {
                    if (ifaceMember is IMethodSymbol method)
                    {
                        if (existingMemberNames.Contains(method.Name))
                        {
                            continue;
                        }

                        if (method.MethodKind != MethodKind.Ordinary)
                        {
                            continue;
                        }

                        var parameters = method.Parameters.Select(p =>
                            new ParameterModel(p.Type.ToDisplayString(), p.Name, p.RefKind)).ToArray();
                        var typeParams = method.TypeParameters.Select(tp => tp.Name).ToArray();

                        methods.Add(new DelegateMethodModel(
                            method.Name,
                            method.ReturnType.ToDisplayString(),
                            method.ReturnType.SpecialType == SpecialType.System_Void,
                            new EquatableArray<ParameterModel>(parameters),
                            new EquatableArray<string>(typeParams)));
                    }
                    else if (ifaceMember is IPropertySymbol propMember)
                    {
                        if (existingMemberNames.Contains(propMember.Name))
                        {
                            continue;
                        }

                        methods.Add(new DelegateMethodModel(
                            propMember.Name,
                            propMember.Type.ToDisplayString(),
                            false,
                            new EquatableArray<ParameterModel>([]),
                            new EquatableArray<string>([]),
                            IsProperty: true,
                            HasGetter: propMember.GetMethod != null,
                            HasSetter: propMember.SetMethod != null));
                    }
                }
            }

            if (methods.Count > 0)
            {
                delegateGroups.Add(new DelegateGroupModel(
                    memberName,
                    interfaceType.ToDisplayString(),
                    new EquatableArray<DelegateMethodModel>(methods.ToArray())));
            }
        }

        if (delegateGroups.Count == 0)
        {
            return Results.Error<DelegateToTypeModel>(new DiagnosticInfo(Diagnostics.DelegateToNoDelegateField, syntax.GetLocation(), symbol.Name));
        }

        return Results.Success(new DelegateToTypeModel(
            ns,
            new EquatableArray<ContainingTypeModel>(containingTypes?.ToArray() ?? []),
            symbol.GetClassName(),
            symbol.IsValueType,
            new EquatableArray<DelegateGroupModel>(delegateGroups.ToArray())));
    }

    // ------------------------------------------------------------
    // Execute
    // ------------------------------------------------------------

    private static void Execute(SourceProductionContext context, ImmutableArray<Result<DelegateToTypeModel>> types)
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

            var filename = MakeFilename(type.Namespace, type.ContainingTypes, type.ClassName, "DelegateTo");
            context.AddSource(filename, SourceText.From(builder.ToString(), Encoding.UTF8));
        }
    }

    private static void BuildSource(SourceBuilder builder, DelegateToTypeModel type)
    {
        var containingTypes = type.ContainingTypes;

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

        var first = true;
        foreach (var group in type.Groups)
        {
            foreach (var method in group.Methods)
            {
                if (!first)
                {
                    builder.NewLine();
                }
                first = false;

                if (method.IsProperty)
                {
                    builder.Indent()
                        .Append("public ")
                        .Append(method.ReturnType)
                        .Append(" ")
                        .Append(method.Name)
                        .NewLine();
                    builder.BeginScope();
                    if (method.HasGetter)
                    {
                        builder.Indent()
                            .Append("get => this.")
                            .Append(group.MemberName)
                            .Append(".")
                            .Append(method.Name)
                            .Append(";")
                            .NewLine();
                    }
                    if (method.HasSetter)
                    {
                        builder.Indent()
                            .Append("set => this.")
                            .Append(group.MemberName)
                            .Append(".")
                            .Append(method.Name)
                            .Append(" = value;")
                            .NewLine();
                    }
                    builder.EndScope();
                }
                else
                {
                    var typeParams = method.TypeParameters;
                    var parameters = method.Parameters;

                    builder.Indent().Append("public ").Append(method.ReturnType).Append(" ").Append(method.Name);

                    if (typeParams.Count > 0)
                    {
                        builder.Append("<").Append(String.Join(", ", typeParams)).Append(">");
                    }

                    builder.Append("(");
                    for (var i = 0; i < parameters.Count; i++)
                    {
                        if (i > 0)
                        {
                            builder.Append(", ");
                        }

                        var p = parameters[i];
                        if (p.RefKind == RefKind.Ref)
                        {
                            builder.Append("ref ");
                        }
                        else if (p.RefKind == RefKind.Out)
                        {
                            builder.Append("out ");
                        }
                        else if (p.RefKind == RefKind.In)
                        {
                            builder.Append("in ");
                        }

                        builder.Append(p.TypeName).Append(" ").Append(p.Name);
                    }
                    builder.Append(")").NewLine();
                    builder.BeginScope();

                    builder.Indent();
                    if (!method.IsVoid)
                    {
                        builder.Append("return ");
                    }

                    builder.Append("this.").Append(group.MemberName).Append(".").Append(method.Name);
                    if (typeParams.Count > 0)
                    {
                        builder.Append("<").Append(String.Join(", ", typeParams)).Append(">");
                    }

                    builder.Append("(");
                    for (var i = 0; i < parameters.Count; i++)
                    {
                        if (i > 0)
                        {
                            builder.Append(", ");
                        }

                        var p = parameters[i];
                        if (p.RefKind == RefKind.Ref)
                        {
                            builder.Append("ref ");
                        }
                        else if (p.RefKind == RefKind.Out)
                        {
                            builder.Append("out ");
                        }
                        else if (p.RefKind == RefKind.In)
                        {
                            builder.Append("in ");
                        }

                        builder.Append(p.Name);
                    }
                    builder.Append(");").NewLine();
                    builder.EndScope();
                }
            }
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

    private sealed record DelegateToTypeModel(
        string Namespace,
        EquatableArray<ContainingTypeModel> ContainingTypes,
        string ClassName,
        bool IsValueType,
        EquatableArray<DelegateGroupModel> Groups);

    private sealed record DelegateGroupModel(
        string MemberName,
        string InterfaceTypeName,
        EquatableArray<DelegateMethodModel> Methods);

    private sealed record DelegateMethodModel(
        string Name,
        string ReturnType,
        bool IsVoid,
        EquatableArray<ParameterModel> Parameters,
        EquatableArray<string> TypeParameters,
        bool IsProperty = false,
        bool HasGetter = false,
        bool HasSetter = false);

    private sealed record ParameterModel(
        string TypeName,
        string Name,
        RefKind RefKind);
}
