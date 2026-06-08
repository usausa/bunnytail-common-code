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
        while (containingSymbol is not null)
        {
            containingTypes ??= [];
            containingTypes.Add(new ContainingTypeModel(containingSymbol.GetClassName(), containingSymbol.IsValueType));
            containingSymbol = containingSymbol.ContainingType;
        }
        containingTypes?.Reverse();

        var delegateGroups = new List<DelegateGroupModel>();
        var diagnostics = new List<DiagnosticInfo>();

        // 既にクラスが実装しているメンバを収集 (手書きで実装しているものはスキップ)
        // メソッドはオーバーロードを区別するためシグネチャ単位で扱う
        // Collect members the class already implements (hand-written implementations are skipped)
        // Methods are handled per signature to distinguish overloads
        var existingSignatures = new HashSet<string>(StringComparer.Ordinal);
        foreach (var existing in symbol.GetMembers())
        {
            if (existing is IMethodSymbol existingMethod && existingMethod.MethodKind == MethodKind.Ordinary && !existingMethod.IsAbstract)
            {
                existingSignatures.Add(MakeMethodSignature(existingMethod));
            }
            else if (existing is IPropertySymbol existingProperty && !existingProperty.IsAbstract)
            {
                existingSignatures.Add("property:" + existingProperty.Name);
            }
        }

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

            if (memberType is null || memberName is null)
            {
                continue;
            }

            // 明示指定された InterfaceType を取得
            // Get the explicitly specified InterfaceType
            var delegateAttr = memberAttrs.First(a => a.AttributeClass?.ToDisplayString() == DelegateToAttributeName);
            var specifiedInterface = GetInterfaceTypeArg(delegateAttr);

            // 委譲対象のインターフェースを解決する
            //  - InterfaceType が指定された場合はその型 (メンバ型が実装しているか検証)
            //  - メンバ型がインターフェースの場合はそのインターフェース
            //  - メンバ型が具象型の場合はその型が実装するインターフェース群
            // Resolve the interfaces to delegate to
            //  - If InterfaceType is specified, that type (verifying the member type implements it)
            //  - If the member type is an interface, that interface
            //  - If the member type is a concrete type, the set of interfaces it implements
            IEnumerable<INamedTypeSymbol> interfaces;
            if (specifiedInterface is not null)
            {
                if ((specifiedInterface.TypeKind != TypeKind.Interface) || !ImplementsInterface(memberType, specifiedInterface))
                {
                    diagnostics.Add(new DiagnosticInfo(
                        Diagnostics.DelegateToInvalidInterfaceType,
                        member.Locations.FirstOrDefault() ?? syntax.GetLocation(),
                        memberName,
                        specifiedInterface.ToDisplayString()));
                    continue;
                }

                interfaces = WithBaseInterfaces(specifiedInterface);
            }
            else if (memberType is INamedTypeSymbol namedMemberType && memberType.TypeKind == TypeKind.Interface)
            {
                interfaces = WithBaseInterfaces(namedMemberType);
            }
            else if (memberType is INamedTypeSymbol)
            {
                interfaces = memberType.AllInterfaces;
            }
            else
            {
                continue;
            }

            var methods = new List<DelegateMethodModel>();
            var seenSignatures = new HashSet<string>(StringComparer.Ordinal);

            foreach (var iface in interfaces)
            {
                foreach (var ifaceMember in iface.GetMembers())
                {
                    if (ifaceMember is IMethodSymbol method)
                    {
                        if (method.MethodKind != MethodKind.Ordinary)
                        {
                            continue;
                        }

                        var methodSignature = MakeMethodSignature(method);
                        if (existingSignatures.Contains(methodSignature))
                        {
                            continue;
                        }

                        if (!seenSignatures.Add(methodSignature))
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
                        var propertySignature = "property:" + propMember.Name;
                        if (existingSignatures.Contains(propertySignature))
                        {
                            continue;
                        }

                        if (!seenSignatures.Add(propertySignature))
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
                            HasGetter: propMember.GetMethod is not null,
                            HasSetter: propMember.SetMethod is not null));
                    }
                }
            }

            if (methods.Count > 0)
            {
                delegateGroups.Add(new DelegateGroupModel(memberName, new EquatableArray<DelegateMethodModel>(methods.ToArray())));
            }
        }

        if (delegateGroups.Count == 0)
        {
            if (diagnostics.Count == 0)
            {
                diagnostics.Add(new DiagnosticInfo(Diagnostics.DelegateToNoDelegateField, syntax.GetLocation(), symbol.Name));
            }

            return new Result<DelegateToTypeModel>(default!, new EquatableArray<DiagnosticInfo>(diagnostics.ToArray()));
        }

        var model = new DelegateToTypeModel(
            ns,
            new EquatableArray<ContainingTypeModel>(containingTypes?.ToArray() ?? []),
            symbol.GetClassName(),
            symbol.IsValueType,
            new EquatableArray<DelegateGroupModel>(delegateGroups.ToArray()));

        return diagnostics.Count == 0
            ? Results.Success(model)
            : new Result<DelegateToTypeModel>(model, new EquatableArray<DiagnosticInfo>(diagnostics.ToArray()));
    }

    private static INamedTypeSymbol? GetInterfaceTypeArg(AttributeData attr)
    {
        var arg = attr.NamedArguments.FirstOrDefault(na => na.Key == "InterfaceType");
        if (arg.Value.IsNull)
        {
            return null;
        }

        return arg.Value.Value as INamedTypeSymbol;
    }

    private static bool ImplementsInterface(ITypeSymbol type, INamedTypeSymbol interfaceType)
    {
        if (SymbolEqualityComparer.Default.Equals(type, interfaceType))
        {
            return true;
        }

        foreach (var iface in type.AllInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(iface, interfaceType))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<INamedTypeSymbol> WithBaseInterfaces(INamedTypeSymbol interfaceType)
    {
        yield return interfaceType;
        foreach (var iface in interfaceType.AllInterfaces)
        {
            yield return iface;
        }
    }

    private static string MakeMethodSignature(IMethodSymbol method)
    {
        var buffer = new StringBuilder();
        buffer.Append(method.Name);
        buffer.Append('`').Append(method.TypeParameters.Length);
        buffer.Append('(');
        for (var i = 0; i < method.Parameters.Length; i++)
        {
            if (i > 0)
            {
                buffer.Append(',');
            }

            buffer.Append(method.Parameters[i].Type.ToDisplayString());
        }
        buffer.Append(')');
        return buffer.ToString();
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
            foreach (var (name, returnType, isVoid, parameters, typeParams, isProperty, hasGetter, hasSetter) in group.Methods)
            {
                if (!first)
                {
                    builder.NewLine();
                }
                first = false;

                if (isProperty)
                {
                    builder.Indent()
                        .Append("public ")
                        .Append(returnType)
                        .Append(" ")
                        .Append(name)
                        .NewLine();
                    builder.BeginScope();
                    if (hasGetter)
                    {
                        builder.Indent()
                            .Append("get => this.")
                            .Append(group.MemberName)
                            .Append(".")
                            .Append(name)
                            .Append(";")
                            .NewLine();
                    }
                    if (hasSetter)
                    {
                        builder.Indent()
                            .Append("set => this.")
                            .Append(group.MemberName)
                            .Append(".")
                            .Append(name)
                            .Append(" = value;")
                            .NewLine();
                    }
                    builder.EndScope();
                }
                else
                {
                    builder.Indent().Append("public ").Append(returnType).Append(" ").Append(name);

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
                    if (!isVoid)
                    {
                        builder.Append("return ");
                    }

                    builder.Append("this.").Append(group.MemberName).Append(".").Append(name);
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
