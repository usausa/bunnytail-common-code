namespace BunnyTail.CommonCode.Generator;

using Microsoft.CodeAnalysis;

internal static class Diagnostics
{
    // ToString
    public static DiagnosticDescriptor InvalidTypeDefinition { get; } = new(
        id: "BTCC0001",
        title: "Invalid type definition",
        messageFormat: "Type must be partial. type=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    // Equality
    public static DiagnosticDescriptor EqualityInvalidTypeDefinition { get; } = new(
        id: "BTCC0101",
        title: "Invalid type definition for GenerateEquality",
        messageFormat: "Type must be partial. type=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor EqualityNoProperties { get; } = new(
        id: "BTCC0102",
        title: "No equality properties found",
        messageFormat: "No public properties found for equality comparison. type=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    // DeepClone
    public static DiagnosticDescriptor DeepCloneInvalidTypeDefinition { get; } = new(
        id: "BTCC0201",
        title: "Invalid type definition for GenerateDeepClone",
        messageFormat: "Type must be partial. type=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor DeepCloneNotImplementIDeepCloneable { get; } = new(
        id: "BTCC0202",
        title: "Type does not implement IDeepCloneable",
        messageFormat: "Type must implement IDeepCloneable<T> to use [GenerateDeepClone]. type=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor DeepClonePropertyMissingDeepClone { get; } = new(
        id: "BTCC0203",
        title: "Property type does not support deep clone",
        messageFormat: "Property type does not implement IDeepCloneable<T>. Use [IgnoreClone] to suppress this warning. property=[{0}], type=[{1}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    // DelegateTo
    public static DiagnosticDescriptor DelegateToInvalidTypeDefinition { get; } = new(
        id: "BTCC0301",
        title: "Invalid type definition for GenerateDelegateTo",
        messageFormat: "Type must be partial. type=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor DelegateToNoDelegateField { get; } = new(
        id: "BTCC0302",
        title: "No [DelegateTo] field or property found",
        messageFormat: "No field or property with [DelegateTo] attribute found. type=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor DelegateToInvalidInterfaceType { get; } = new(
        id: "BTCC0303",
        title: "Invalid InterfaceType for [DelegateTo]",
        messageFormat: "InterfaceType must be an interface implemented by the delegate member type. member=[{0}], interfaceType=[{1}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    // CompareTo
    public static DiagnosticDescriptor CompareToInvalidTypeDefinition { get; } = new(
        id: "BTCC0401",
        title: "Invalid type definition for GenerateCompareTo",
        messageFormat: "Type must be partial. type=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor CompareToNoKeys { get; } = new(
        id: "BTCC0402",
        title: "No [CompareKey] properties found",
        messageFormat: "No properties with [CompareKey] attribute found. type=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}
