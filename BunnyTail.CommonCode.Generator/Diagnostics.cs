namespace BunnyTail.CommonCode.Generator;

using Microsoft.CodeAnalysis;

internal static class Diagnostics
{
    // ToString (01xx)
    public static DiagnosticDescriptor InvalidTypeDefinition { get; } = new(
        id: "BTCC0101",
        title: "Invalid type definition",
        messageFormat: "Type must be partial. type=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

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

    // Equality (02xx)
    public static DiagnosticDescriptor EqualityInvalidTypeDefinition { get; } = new(
        id: "BTCC0201",
        title: "Invalid type for GenerateEquality",
        messageFormat: "Type must be partial. type=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor EqualityNoProperties { get; } = new(
        id: "BTCC0202",
        title: "No equality properties found",
        messageFormat: "No public properties for equality. type=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    // DeepClone (03xx)
    public static DiagnosticDescriptor DeepCloneInvalidTypeDefinition { get; } = new(
        id: "BTCC0301",
        title: "Invalid type for GenerateDeepClone",
        messageFormat: "Type must be partial. type=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor DeepCloneNotImplementIDeepCloneable { get; } = new(
        id: "BTCC0302",
        title: "Type does not implement IDeepCloneable",
        messageFormat: "Type must implement IDeepCloneable<T>. type=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor DeepClonePropertyMissingDeepClone { get; } = new(
        id: "BTCC0303",
        title: "Property type is not deep cloneable",
        messageFormat: "Property type is not IDeepCloneable<T>. property=[{0}], type=[{1}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    // DelegateTo (04xx)
    public static DiagnosticDescriptor DelegateToInvalidTypeDefinition { get; } = new(
        id: "BTCC0401",
        title: "Invalid type for GenerateDelegateTo",
        messageFormat: "Type must be partial. type=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor DelegateToNoDelegateField { get; } = new(
        id: "BTCC0402",
        title: "No [DelegateTo] field or property found",
        messageFormat: "No member has [DelegateTo]. type=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor DelegateToInvalidInterfaceType { get; } = new(
        id: "BTCC0403",
        title: "Invalid InterfaceType for [DelegateTo]",
        messageFormat: "InterfaceType is not implemented. member=[{0}], interfaceType=[{1}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    // CompareTo (05xx)
    public static DiagnosticDescriptor CompareToInvalidTypeDefinition { get; } = new(
        id: "BTCC0501",
        title: "Invalid type for GenerateCompareTo",
        messageFormat: "Type must be partial. type=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor CompareToNoKeys { get; } = new(
        id: "BTCC0502",
        title: "No [CompareKey] properties found",
        messageFormat: "No property has [CompareKey]. type=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
