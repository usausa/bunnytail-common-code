namespace BunnyTail.CommonCode.Generator;

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using SourceGenerateHelper;

[Generator]
public sealed class DelegateToGenerator : IIncrementalGenerator
{
    private const string GenerateAttributeName = "BunnyTail.CommonCode.GenerateDelegateToAttribute";
    private const string DelegateToAttributeName = "BunnyTail.CommonCode.DelegateToAttribute";

    private static readonly string[] NullableAttributeNames =
    [
        "System.Diagnostics.CodeAnalysis.AllowNullAttribute",
        "System.Diagnostics.CodeAnalysis.DisallowNullAttribute",
        "System.Diagnostics.CodeAnalysis.MaybeNullAttribute",
        "System.Diagnostics.CodeAnalysis.NotNullAttribute",
        "System.Diagnostics.CodeAnalysis.MaybeNullWhenAttribute",
        "System.Diagnostics.CodeAnalysis.NotNullWhenAttribute",
        "System.Diagnostics.CodeAnalysis.NotNullIfNotNullAttribute"
    ];

    // ------------------------------------------------------------
    // Initialize
    // ------------------------------------------------------------

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var targetProvider = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                GenerateAttributeName,
                static (node, _) => IsTypeSyntax(node),
                static (ctx, _) => GetTypeModel(ctx))
            .Collect();
        var treeProvider = context.ForAttributeWithMetadataNameSyntaxTrees(
            GenerateAttributeName,
            static (node, _) => IsTypeSyntax(node));

        context.RegisterSourceOutput(
            targetProvider.Combine(treeProvider),
            static (spc, input) => spc.ReportDiagnostics(GeneratedTypes.SelectDiagnostics(input.Left, "GenerateDelegateTo").Distinct(), input.Right));

        var models = targetProvider
            .SelectMany(static (x, _) => GeneratedTypes.SelectTypes(x))
            .WithTrackingName("Models");
        context.RegisterImplementationSourceOutput(
            models,
            static (spc, type) => Execute(spc, type));
    }

    private static bool IsTypeSyntax(SyntaxNode node) =>
        node is ClassDeclarationSyntax or StructDeclarationSyntax or RecordDeclarationSyntax;

    private static Result<TypeModel> GetTypeModel(GeneratorAttributeSyntaxContext context)
    {
        var syntax = (TypeDeclarationSyntax)context.TargetNode;
        var symbol = (INamedTypeSymbol)context.TargetSymbol;

        if (!GeneratedTypes.IsExtendable(syntax, symbol))
        {
            return Results.Error<TypeModel>(new DiagnosticInfo(Diagnostics.DelegateToInvalidTypeDefinition, syntax.Identifier.GetLocation(), symbol.Name));
        }

        var ns = String.IsNullOrEmpty(symbol.ContainingNamespace.Name) ? string.Empty : symbol.ContainingNamespace.ToDisplayString();

        var containingTypes = symbol.GetContainingTypes()
            .Select(static x => new ContainingTypeModel(x.GetClassName(), x.GetDeclarationKeyword()))
            .ToArray();

        var compilation = context.SemanticModel.Compilation;
        var members = new List<MemberModel>();
        var diagnostics = new List<DiagnosticInfo>();
        var handled = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        var generated = new List<ISymbol>();
        var hasDelegate = false;

        foreach (var member in symbol.GetMembers())
        {
            var (memberType, delegateAttr) = member switch
            {
                IFieldSymbol field => (field.Type, field.FindAttribute(DelegateToAttributeName)),
                IPropertySymbol property => (property.Type, property.FindAttribute(DelegateToAttributeName)),
                _ => (null, null)
            };
            if ((memberType is null) || (delegateAttr is null))
            {
                continue;
            }

            hasDelegate = true;

            IEnumerable<INamedTypeSymbol> interfaces;
            if (delegateAttr.TryGetNamedArgument<INamedTypeSymbol>("InterfaceType", out var specifiedInterface))
            {
                if ((specifiedInterface.TypeKind != TypeKind.Interface) || !ImplementsInterface(memberType, specifiedInterface))
                {
                    diagnostics.Add(new DiagnosticInfo(
                        Diagnostics.DelegateToInvalidInterfaceType,
                        member.Locations.FirstOrDefault() ?? syntax.Identifier.GetLocation(),
                        member.Name,
                        specifiedInterface.ToDisplayString()));
                    continue;
                }

                interfaces = WithBaseInterfaces(specifiedInterface);
            }
            else if ((memberType is INamedTypeSymbol namedMemberType) && (memberType.TypeKind == TypeKind.Interface))
            {
                interfaces = WithBaseInterfaces(namedMemberType);
            }
            else if (memberType is INamedTypeSymbol or ITypeParameterSymbol)
            {
                interfaces = GetAllInterfaces(memberType);
            }
            else
            {
                continue;
            }

            var target = "this." + CSharpIdentifier.Escape(member.Name);

            foreach (var iface in interfaces.OrderByDescending(static x => x.AllInterfaces.Length))
            {
                var interfaceName = iface.ToDisplayString(SymbolDisplayFormats.FullyQualifiedNullable);
                var implementsInterface = symbol.AllInterfaces.Contains(iface, SymbolEqualityComparer.Default);
                foreach (var interfaceMember in iface.GetMembers())
                {
                    if (!IsDelegatable(interfaceMember) || !handled.Add(interfaceMember) || IsImplemented(symbol, interfaceMember, compilation))
                    {
                        continue;
                    }

                    var explicitInterface = default(string?);
                    var conflict = generated.FirstOrDefault(x => HasSameSignature(x, interfaceMember, compilation));
                    if (conflict is not null)
                    {
                        if (HasSameShape(conflict, interfaceMember, compilation))
                        {
                            continue;
                        }

                        if (!implementsInterface || (interfaceMember is IMethodSymbol { IsGenericMethod: true }))
                        {
                            continue;
                        }

                        explicitInterface = interfaceName;
                    }

                    var receiver = (explicitInterface is not null) || NeedsInterfaceCast(memberType, interfaceMember)
                        ? "((" + interfaceName + ")" + target + ")"
                        : target;
                    var lines = interfaceMember switch
                    {
                        IMethodSymbol method => RenderMethod(method, explicitInterface, receiver),
                        IPropertySymbol property => RenderProperty(property, explicitInterface, receiver, implementsInterface),
                        IEventSymbol @event => RenderEvent(@event, explicitInterface, receiver),
                        _ => null
                    };
                    if (lines is null)
                    {
                        continue;
                    }

                    members.Add(new MemberModel(new EquatableArray<string>(lines.ToArray())));
                    if (explicitInterface is null)
                    {
                        generated.Add(interfaceMember);
                    }
                }
            }
        }

        if (!hasDelegate)
        {
            diagnostics.Add(new DiagnosticInfo(Diagnostics.DelegateToNoDelegateField, syntax.Identifier.GetLocation(), symbol.Name));
        }

        if (members.Count == 0)
        {
            return new Result<TypeModel>(default!, new EquatableArray<DiagnosticInfo>(diagnostics));
        }

        var className = symbol.GetClassName();
        return new Result<TypeModel>(
            new TypeModel(
                ns,
                new EquatableArray<ContainingTypeModel>(containingTypes),
                className,
                symbol.GetDeclarationKeyword(),
                new EquatableArray<MemberModel>(members),
                HintNameBuilder.Build(ns, [.. containingTypes.Select(static x => x.ClassName), className, "DelegateTo"]),
                symbol.ToDisplayString()),
            new EquatableArray<DiagnosticInfo>(diagnostics));
    }

    private static bool ImplementsInterface(ITypeSymbol type, INamedTypeSymbol interfaceType)
    {
        if (SymbolEqualityComparer.Default.Equals(type, interfaceType))
        {
            return true;
        }

        foreach (var iface in GetAllInterfaces(type))
        {
            if (SymbolEqualityComparer.Default.Equals(iface, interfaceType))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<INamedTypeSymbol> GetAllInterfaces(ITypeSymbol type) =>
        type is ITypeParameterSymbol typeParameter
            ? typeParameter.ConstraintTypes
                .SelectMany(static x => x is INamedTypeSymbol { TypeKind: TypeKind.Interface } named ? WithBaseInterfaces(named) : GetAllInterfaces(x))
                .Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default)
            : type.AllInterfaces;

    private static IEnumerable<INamedTypeSymbol> WithBaseInterfaces(INamedTypeSymbol interfaceType)
    {
        yield return interfaceType;
        foreach (var iface in interfaceType.AllInterfaces)
        {
            yield return iface;
        }
    }

    private static bool IsDelegatable(ISymbol member) =>
        !member.IsStatic &&
        (member.DeclaredAccessibility == Accessibility.Public) &&
        (member.IsAbstract || member.IsVirtual) &&
        member is IMethodSymbol { MethodKind: MethodKind.Ordinary } or IPropertySymbol or IEventSymbol;

    private static bool IsImplemented(INamedTypeSymbol type, ISymbol interfaceMember, Compilation compilation)
    {
        var implementation = type.FindImplementationForInterfaceMember(interfaceMember);
        if ((implementation is not null) && (implementation.ContainingType.TypeKind != TypeKind.Interface))
        {
            return true;
        }

        for (var current = type; current is not null; current = current.BaseType)
        {
            var isBase = !SymbolEqualityComparer.Default.Equals(current, type);
            foreach (var candidate in current.GetMembers(interfaceMember.Name))
            {
                if ((!isBase || (candidate.DeclaredAccessibility != Accessibility.Private)) &&
                    HasSameSignature(candidate, interfaceMember, compilation))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HasSameSignature(ISymbol x, ISymbol y, Compilation compilation)
    {
        if (x.Name != y.Name)
        {
            return false;
        }

        return (x, y) switch
        {
            (IMethodSymbol xm, IMethodSymbol ym) => (xm.Arity == ym.Arity) && HasSameParameters(xm.Parameters, ConstructLike(ym, xm).Parameters, compilation),
            (IPropertySymbol { IsIndexer: true } xp, IPropertySymbol { IsIndexer: true } yp) => HasSameParameters(xp.Parameters, yp.Parameters, compilation),
            _ => true
        };
    }

    private static bool HasSameShape(ISymbol x, ISymbol y, Compilation compilation) =>
        (x, y) switch
        {
            (IMethodSymbol xm, IMethodSymbol ym) =>
                (xm.RefKind == ym.RefKind) && IsIdentity(xm.ReturnType, ConstructLike(ym, xm).ReturnType, compilation),
            (IPropertySymbol xp, IPropertySymbol yp) =>
                (xp.RefKind == yp.RefKind) && IsIdentity(xp.Type, yp.Type, compilation) && HasAccessorsOf(xp, yp),
            (IEventSymbol xe, IEventSymbol ye) => IsIdentity(xe.Type, ye.Type, compilation),
            _ => false
        };

    private static bool HasAccessorsOf(IPropertySymbol property, IPropertySymbol other)
    {
        if ((other.GetMethod is not null) && (property.GetMethod is null))
        {
            return false;
        }

        return (other.SetMethod is null) ||
               ((property.SetMethod is not null) && (property.SetMethod.IsInitOnly == other.SetMethod.IsInitOnly));
    }

    private static IMethodSymbol ConstructLike(IMethodSymbol method, IMethodSymbol other) =>
        (method.Arity > 0) && (method.Arity == other.Arity) ? method.Construct([.. other.TypeParameters]) : method;

    private static bool HasSameParameters(ImmutableArray<IParameterSymbol> x, ImmutableArray<IParameterSymbol> y, Compilation compilation)
    {
        if (x.Length != y.Length)
        {
            return false;
        }

        for (var i = 0; i < x.Length; i++)
        {
            if (((x[i].RefKind == RefKind.None) != (y[i].RefKind == RefKind.None)) || !IsIdentity(x[i].Type, y[i].Type, compilation))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsIdentity(ITypeSymbol x, ITypeSymbol y, Compilation compilation) =>
        compilation.ClassifyCommonConversion(x, y).IsIdentity;

    private static bool NeedsInterfaceCast(ITypeSymbol memberType, ISymbol interfaceMember)
    {
        if (memberType.TypeKind is TypeKind.Interface or TypeKind.TypeParameter)
        {
            return false;
        }

        return memberType.FindImplementationForInterfaceMember(interfaceMember) switch
        {
            IMethodSymbol method => (method.MethodKind == MethodKind.ExplicitInterfaceImplementation) || (method.ContainingType.TypeKind == TypeKind.Interface),
            IPropertySymbol property => !property.ExplicitInterfaceImplementations.IsEmpty || (property.ContainingType.TypeKind == TypeKind.Interface),
            IEventSymbol @event => !@event.ExplicitInterfaceImplementations.IsEmpty || (@event.ContainingType.TypeKind == TypeKind.Interface),
            _ => true
        };
    }

    // ------------------------------------------------------------
    // Member
    // ------------------------------------------------------------

    private static List<string> RenderMethod(IMethodSymbol method, string? explicitInterface, string receiver)
    {
        var lines = new List<string>();
        lines.AddRange(RenderAttributes(method.GetReturnTypeAttributes(), "return: "));

        var header = new StringBuilder();
        if (explicitInterface is null)
        {
            header.Append("public ");
        }

        header
            .Append(RenderRefKind(method.RefKind))
            .Append(method.ReturnType.ToDisplayString(SymbolDisplayFormats.FullyQualifiedNullable))
            .Append(' ');
        if (explicitInterface is not null)
        {
            header.Append(explicitInterface).Append('.');
        }

        header
            .Append(CSharpIdentifier.Escape(method.Name))
            .Append(RenderTypeParameters(method))
            .Append('(')
            .Append(String.Join(", ", method.Parameters.Select(x => RenderParameter(x, explicitInterface is null))))
            .Append(')');
        lines.Add(header.ToString());

        if (explicitInterface is null)
        {
            lines.AddRange(RenderConstraints(method.TypeParameters).Select(static x => "    " + x));
        }

        var call = receiver + "." + CSharpIdentifier.Escape(method.Name) + RenderTypeParameters(method) +
                   "(" + String.Join(", ", method.Parameters.Select(RenderArgument)) + ")";
        lines.Add("{");
        lines.Add("    " + (method.ReturnsVoid ? string.Empty : method.RefKind == RefKind.None ? "return " : "return ref ") + call + ";");
        lines.Add("}");
        return lines;
    }

    private static List<string>? RenderProperty(IPropertySymbol property, string? explicitInterface, string receiver, bool implementsInterface)
    {
        var initOnly = property.SetMethod is { IsInitOnly: true };
        if ((initOnly && implementsInterface) || (property.GetMethod is null && initOnly))
        {
            return null;
        }

        var lines = new List<string>();
        lines.AddRange(RenderAttributes(property.GetAttributes(), string.Empty));

        var header = new StringBuilder();
        if (explicitInterface is null)
        {
            header.Append("public ");
        }

        header
            .Append(RenderRefKind(property.RefKind))
            .Append(property.Type.ToDisplayString(SymbolDisplayFormats.FullyQualifiedNullable))
            .Append(' ');
        if (explicitInterface is not null)
        {
            header.Append(explicitInterface).Append('.');
        }

        string access;
        if (property.IsIndexer)
        {
            header.Append("this[").Append(String.Join(", ", property.Parameters.Select(x => RenderParameter(x, explicitInterface is null)))).Append(']');
            access = receiver + "[" + String.Join(", ", property.Parameters.Select(RenderArgument)) + "]";
        }
        else
        {
            header.Append(CSharpIdentifier.Escape(property.Name));
            access = receiver + "." + CSharpIdentifier.Escape(property.Name);
        }

        lines.Add(header.ToString());
        lines.Add("{");
        if (property.GetMethod is not null)
        {
            lines.Add("    get => " + (property.RefKind == RefKind.None ? string.Empty : "ref ") + access + ";");
        }

        if ((property.SetMethod is not null) && !initOnly)
        {
            lines.Add("    set => " + access + " = value;");
        }

        lines.Add("}");
        return lines;
    }

    private static List<string> RenderEvent(IEventSymbol @event, string? explicitInterface, string receiver)
    {
        var header = new StringBuilder();
        if (explicitInterface is null)
        {
            header.Append("public ");
        }

        header
            .Append("event ")
            .Append(@event.Type.ToDisplayString(SymbolDisplayFormats.FullyQualifiedNullable))
            .Append(' ');
        if (explicitInterface is not null)
        {
            header.Append(explicitInterface).Append('.');
        }

        header.Append(CSharpIdentifier.Escape(@event.Name));

        var access = receiver + "." + CSharpIdentifier.Escape(@event.Name);
        return
        [
            header.ToString(),
            "{",
            "    add => " + access + " += value;",
            "    remove => " + access + " -= value;",
            "}"
        ];
    }

    private static string RenderRefKind(RefKind refKind) =>
        refKind switch
        {
            RefKind.Ref => "ref ",
            RefKind.RefReadOnly => "ref readonly ",
            _ => string.Empty
        };

    private static string RenderTypeParameters(IMethodSymbol method) =>
        method.Arity > 0 ? "<" + String.Join(", ", method.TypeParameters.Select(static x => CSharpIdentifier.Escape(x.Name))) + ">" : string.Empty;

    private static IEnumerable<string> RenderConstraints(ImmutableArray<ITypeParameterSymbol> typeParameters)
    {
        foreach (var typeParameter in typeParameters)
        {
            var constraints = new List<string>();
            if (typeParameter.HasUnmanagedTypeConstraint)
            {
                constraints.Add("unmanaged");
            }
            else if (typeParameter.HasValueTypeConstraint)
            {
                constraints.Add("struct");
            }
            else if (typeParameter.HasReferenceTypeConstraint)
            {
                constraints.Add(typeParameter.ReferenceTypeConstraintNullableAnnotation == NullableAnnotation.Annotated ? "class?" : "class");
            }
            else if (typeParameter.HasNotNullConstraint)
            {
                constraints.Add("notnull");
            }

            constraints.AddRange(typeParameter.ConstraintTypes.Select(static x => x.ToDisplayString(SymbolDisplayFormats.FullyQualifiedNullable)));

            if (typeParameter.HasConstructorConstraint)
            {
                constraints.Add("new()");
            }

            if (typeParameter.AllowsRefLikeType)
            {
                constraints.Add("allows ref struct");
            }

            if (constraints.Count > 0)
            {
                yield return "where " + CSharpIdentifier.Escape(typeParameter.Name) + " : " + String.Join(", ", constraints);
            }
        }
    }

    private static string RenderParameter(IParameterSymbol parameter, bool withDefaultValue)
    {
        var buffer = new StringBuilder();
        foreach (var attribute in RenderAttributes(parameter.GetAttributes(), string.Empty))
        {
            buffer.Append(attribute).Append(' ');
        }

        if ((parameter.ScopedKind != ScopedKind.None) && (parameter.RefKind != RefKind.Out))
        {
            buffer.Append("scoped ");
        }

        if (parameter.IsParams)
        {
            buffer.Append("params ");
        }

        buffer.Append(parameter.RefKind switch
        {
            RefKind.Ref => "ref ",
            RefKind.Out => "out ",
            RefKind.In => "in ",
            RefKind.RefReadOnlyParameter => "ref readonly ",
            _ => string.Empty
        });
        buffer
            .Append(parameter.Type.ToDisplayString(SymbolDisplayFormats.FullyQualifiedNullable))
            .Append(' ')
            .Append(CSharpIdentifier.Escape(parameter.Name));

        if (withDefaultValue && parameter.HasExplicitDefaultValue)
        {
            buffer.Append(" = ").Append(parameter.GetDefaultValueExpression() ?? "default");
        }

        return buffer.ToString();
    }

    private static string RenderArgument(IParameterSymbol parameter) =>
        parameter.RefKind switch
        {
            RefKind.Ref => "ref ",
            RefKind.Out => "out ",
            RefKind.In or RefKind.RefReadOnlyParameter => "in ",
            _ => string.Empty
        } + CSharpIdentifier.Escape(parameter.Name);

    private static IEnumerable<string> RenderAttributes(ImmutableArray<AttributeData> attributes, string target)
    {
        foreach (var attribute in attributes)
        {
            var attributeClass = attribute.AttributeClass;
            if ((attributeClass is null) || !NullableAttributeNames.Any(attributeClass.HasFullyQualifiedMetadataName))
            {
                continue;
            }

            var arguments = attribute.ConstructorArguments.Select(static x => CSharpLiteral.Format(x.Value)).ToList();
            if (arguments.Any(static x => x is null))
            {
                continue;
            }

            yield return "[" + target + attributeClass.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) +
                         (arguments.Count > 0 ? "(" + String.Join(", ", arguments) + ")" : string.Empty) + "]";
        }
    }

    // ------------------------------------------------------------
    // Generator
    // ------------------------------------------------------------

    private static void Execute(SourceProductionContext context, TypeModel type)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        var builder = new SourceBuilder();
        BuildSource(builder, type);

        context.AddSource(type.HintName, builder);
    }

    private static void BuildSource(SourceBuilder builder, TypeModel type)
    {
        var containingTypes = type.ContainingTypes;

        builder.AutoGenerated();
        builder.EnableNullable();
        builder.Disable("CS0612, CS0618");
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
                .Append(ct.Keyword)
                .Append(" ")
                .Append(ct.ClassName)
                .NewLine();
            builder.BeginScope();
        }

        builder.Indent()
            .Append("partial ")
            .Append(type.Keyword)
            .Append(" ")
            .Append(type.ClassName)
            .NewLine();
        builder.BeginScope();

        var first = true;
        foreach (var member in type.Members)
        {
            if (!first)
            {
                builder.NewLine();
            }
            first = false;

            foreach (var line in member.Lines)
            {
                builder.Indent().Append(line).NewLine();
            }
        }

        builder.EndScope();

        for (var i = 0; i < containingTypes.Count; i++)
        {
            builder.EndScope();
        }
    }

    // ------------------------------------------------------------
    // Model
    // ------------------------------------------------------------

    private sealed record ContainingTypeModel(
        string ClassName,
        string Keyword);

    private sealed record MemberModel(
        EquatableArray<string> Lines);

    private sealed record TypeModel(
        string Namespace,
        EquatableArray<ContainingTypeModel> ContainingTypes,
        string ClassName,
        string Keyword,
        EquatableArray<MemberModel> Members,
        string HintName,
        string DisplayName) : IGeneratedType;
}
