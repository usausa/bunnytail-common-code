namespace BunnyTail.CommonCode.Generator;

using Microsoft.CodeAnalysis;

using SourceGenerateHelper;

internal static class Diagnostics
{
    // Common (00xx)

    public static DiagnosticDescriptor HintNameCollision { get; } = new(
        id: "BTCC0001",
        title: "Type name differs only in case",
        messageFormat: "[{0}] type name differs only in case from another type, and its source is not generated. type=[{1}], other=[{2}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: DiagnosticTags.NotSuppressible);

    // ToString (01xx)

    public static DiagnosticDescriptor InvalidTypeDefinition { get; } = new(
        id: "BTCC0101",
        title: "Invalid type definition",
        messageFormat: "[GenerateToString] type and its containing types must be partial, and the type must not be file-local. type=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: DiagnosticTags.NotSuppressible);

    public static DiagnosticDescriptor ToStringFormatOnIgnored { get; } = new(
        id: "BTCC0102",
        title: "ToStringFormat on an ignored member",
        messageFormat: "Member is excluded by [IgnoreToString]. member=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor ToStringMaskConflict { get; } = new(
        id: "BTCC0103",
        title: "Conflicting mask settings",
        messageFormat: "MaskChar is overridden by MaskPattern. member=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor ToStringFormatNoEffect { get; } = new(
        id: "BTCC0104",
        title: "ToStringFormat has no effect",
        messageFormat: "[ToStringFormat] has no effective setting. member=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor InvalidPropertyValue { get; } = new(
        id: "BTCC0105",
        title: "Invalid MSBuild property value",
        messageFormat: "MSBuild property value is not valid, and the default is used. property=[{0}], value=[{1}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor ToStringSealed { get; } = new(
        id: "BTCC0106",
        title: "ToString sealed in a base type",
        messageFormat: "ToString is sealed in a base type, and cannot be generated. type=[{0}], base=[{1}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: DiagnosticTags.NotSuppressible);

    public static DiagnosticDescriptor ToStringRefStructMember { get; } = new(
        id: "BTCC0107",
        title: "Member of a ref struct type",
        messageFormat: "Member of a ref struct type cannot be written, exclude it with [IgnoreToString]. member=[{0}], type=[{1}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: DiagnosticTags.NotSuppressible);

    // Equality (02xx)

    public static DiagnosticDescriptor EqualityInvalidTypeDefinition { get; } = new(
        id: "BTCC0201",
        title: "Invalid type for GenerateEquality",
        messageFormat: "[GenerateEquality] type and its containing types must be partial, and the type must not be file-local. type=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: DiagnosticTags.NotSuppressible);

    public static DiagnosticDescriptor EqualityNoProperties { get; } = new(
        id: "BTCC0202",
        title: "No equality properties found",
        messageFormat: "No public properties for equality. type=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: DiagnosticTags.NotSuppressible);

    public static DiagnosticDescriptor EqualityRefStructMember { get; } = new(
        id: "BTCC0203",
        title: "Property of a ref struct type",
        messageFormat: "Property of a ref struct type cannot be compared, exclude it with [IgnoreEquality]. property=[{0}], type=[{1}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: DiagnosticTags.NotSuppressible);

    // DeepClone (03xx)

    public static DiagnosticDescriptor DeepCloneInvalidTypeDefinition { get; } = new(
        id: "BTCC0301",
        title: "Invalid type for GenerateDeepClone",
        messageFormat: "[GenerateDeepClone] type and its containing types must be partial, and the type must not be file-local. type=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: DiagnosticTags.NotSuppressible);

    public static DiagnosticDescriptor DeepCloneNotImplementIDeepCloneable { get; } = new(
        id: "BTCC0302",
        title: "Type does not implement IDeepCloneable",
        messageFormat: "[GenerateDeepClone] type must implement IDeepCloneable<T>. type=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: DiagnosticTags.NotSuppressible);

    public static DiagnosticDescriptor DeepClonePropertyMissingDeepClone { get; } = new(
        id: "BTCC0303",
        title: "Property type is not deep cloneable",
        messageFormat: "Property type is not IDeepCloneable<T>. property=[{0}], type=[{1}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor DeepCloneRefStruct { get; } = new(
        id: "BTCC0304",
        title: "GenerateDeepClone on a ref struct",
        messageFormat: "[GenerateDeepClone] cannot be used on a ref struct. type=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: DiagnosticTags.NotSuppressible);

    // DelegateTo (04xx)

    public static DiagnosticDescriptor DelegateToInvalidTypeDefinition { get; } = new(
        id: "BTCC0401",
        title: "Invalid type for GenerateDelegateTo",
        messageFormat: "[GenerateDelegateTo] type and its containing types must be partial, and the type must not be file-local. type=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: DiagnosticTags.NotSuppressible);

    public static DiagnosticDescriptor DelegateToNoDelegateField { get; } = new(
        id: "BTCC0402",
        title: "No [DelegateTo] field or property found",
        messageFormat: "No member has [DelegateTo]. type=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: DiagnosticTags.NotSuppressible);

    public static DiagnosticDescriptor DelegateToInvalidInterfaceType { get; } = new(
        id: "BTCC0403",
        title: "Invalid InterfaceType for [DelegateTo]",
        messageFormat: "[DelegateTo] InterfaceType is not implemented. member=[{0}], interfaceType=[{1}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: DiagnosticTags.NotSuppressible);

    // CompareTo (05xx)

    public static DiagnosticDescriptor CompareToInvalidTypeDefinition { get; } = new(
        id: "BTCC0501",
        title: "Invalid type for GenerateCompareTo",
        messageFormat: "[GenerateCompareTo] type and its containing types must be partial, and the type must not be file-local. type=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: DiagnosticTags.NotSuppressible);

    public static DiagnosticDescriptor CompareToNoKeys { get; } = new(
        id: "BTCC0502",
        title: "No [CompareKey] properties found",
        messageFormat: "No property has [CompareKey]. type=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: DiagnosticTags.NotSuppressible);

    public static DiagnosticDescriptor CompareToKeyNotComparable { get; } = new(
        id: "BTCC0503",
        title: "Key type not comparable",
        messageFormat: "Key type implements neither IComparable<T> nor IComparable, or is a ref struct, and cannot be compared. member=[{0}], type=[{1}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: DiagnosticTags.NotSuppressible);
}
