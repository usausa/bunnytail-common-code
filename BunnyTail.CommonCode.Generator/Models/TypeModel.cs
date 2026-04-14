namespace BunnyTail.CommonCode.Generator.Models;

using SourceGenerateHelper;

internal sealed record ContainingTypeModel(
    string ClassName,
    bool IsValueType);

internal sealed record TypeModel(
    string Namespace,
    EquatableArray<ContainingTypeModel> ContainingTypes,
    string ClassName,
    bool IsValueType,
    EquatableArray<PropertyModel> Properties);
